using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.ClassCatalog;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class CoachReviewService(AppDbContext db, TimeProvider clock) : ICoachReviewService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<CoachReviewDTO> CreateAsync(
        int actorUserId, int coachId, CoachReviewRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);

        return await ExecuteTransactionAsync(async () =>
        {
            var member = await ResolveActiveMemberAsync(actorUserId, ct);
            var coach = await ResolveActiveCoachAsync(coachId, ct);

            if (!await HasTrainedWithCoachAsync(member.Id, coachId, ct))
            {
                throw new BusinessException(
                    409,
                    "Chỉ hội viên đã tham gia buổi học hoặc có giáo án với huấn luyện viên này mới được đánh giá.");
            }

            var exists = await db.CoachReviews
                .AnyAsync(r => r.CoachId == coachId && r.MemberId == member.Id, ct);
            if (exists)
                throw new BusinessException(409, "Bạn đã đánh giá huấn luyện viên này.");

            var now = Now;
            var review = new CoachReview
            {
                CoachId = coachId,
                MemberId = member.Id,
                Rating = request.Rating,
                Comment = NormalizeComment(request.Comment),
                CreatedAt = now
            };

            db.CoachReviews.Add(review);
            db.Notifications.Add(new Notification
            {
                UserId = coach.UserId,
                Title = "Bạn nhận được đánh giá mới",
                Content = $"{member.User.FullName} đã đánh giá {request.Rating}/5 sao cho bạn.",
                SentAt = now
            });

            await db.SaveChangesAsync(ct);

            db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = "Create",
                EntityName = nameof(CoachReview),
                EntityId = review.Id,
                Details = $"Reviewed coach {coachId} with {review.Rating} stars",
                Timestamp = now
            });

            return ToDto(review, member.User.FullName);
        }, ct);
    }

    public async Task<CoachReviewDTO> UpdateMineAsync(
        int actorUserId, int coachId, CoachReviewRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);

        return await ExecuteTransactionAsync(async () =>
        {
            var member = await ResolveActiveMemberAsync(actorUserId, ct);
            await ResolveActiveCoachAsync(coachId, ct);

            var review = await db.CoachReviews
                .SingleOrDefaultAsync(r => r.CoachId == coachId && r.MemberId == member.Id, ct)
                ?? throw new BusinessException(404, "Bạn chưa đánh giá huấn luyện viên này.");

            var now = Now;
            review.Rating = request.Rating;
            review.Comment = NormalizeComment(request.Comment);
            review.UpdatedAt = now;

            db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = "Update",
                EntityName = nameof(CoachReview),
                EntityId = review.Id,
                Details = $"Updated coach {coachId} review to {review.Rating} stars",
                Timestamp = now
            });

            return ToDto(review, member.User.FullName);
        }, ct);
    }

    public async Task<CoachReviewSummaryDTO> GetByCoachAsync(
        int coachId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        ValidatePagination(page, pageSize);
        var coach = await db.Coaches
            .AsNoTracking()
            .Include(c => c.User)
            .SingleOrDefaultAsync(c => c.Id == coachId && c.User.IsActive && c.User.Role == UserRoles.Coach, ct)
            ?? throw new BusinessException(404, "Không tìm thấy huấn luyện viên.");

        var baseQuery = db.CoachReviews
            .AsNoTracking()
            .Where(r => r.CoachId == coachId);

        var total = await baseQuery.CountAsync(ct);
        double? avg = total > 0
            ? Math.Round(await baseQuery.AverageAsync(r => (double)r.Rating, ct), 1)
            : null;

        var breakdown = await baseQuery
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var counts = Enumerable.Range(1, 5)
            .ToDictionary(star => star, star => breakdown.FirstOrDefault(x => x.Rating == star)?.Count ?? 0);

        var items = await baseQuery
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new CoachReviewDTO(
                r.Id, r.CoachId, r.MemberId, r.Member.User.FullName,
                r.Rating, r.Comment, r.CreatedAt, r.UpdatedAt))
            .ToListAsync(ct);

        return new CoachReviewSummaryDTO(
            coach.Id, coach.User.FullName, avg, total, counts, items, total, page, pageSize);
    }

    public async Task<CoachReviewSummaryDTO> GetMineAsync(
        int actorUserId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var coachId = await db.Coaches
            .AsNoTracking()
            .Where(c => c.UserId == actorUserId && c.User.IsActive && c.User.Role == UserRoles.Coach)
            .Select(c => (int?)c.Id)
            .SingleOrDefaultAsync(ct)
            ?? throw new BusinessException(404, "Không tìm thấy hồ sơ huấn luyện viên.");

        return await GetByCoachAsync(coachId, page, pageSize, ct);
    }

    private async Task<Member> ResolveActiveMemberAsync(int actorUserId, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == actorUserId && u.IsActive, ct)
            ?? throw new BusinessException(401, "Tài khoản không hoạt động. Vui lòng đăng nhập lại.");

        if (actor.Role != UserRoles.Member)
            throw new BusinessException(403, "Chỉ hội viên được đánh giá huấn luyện viên.");

        return await db.Members
            .Include(m => m.User)
            .SingleOrDefaultAsync(m => m.UserId == actorUserId, ct)
            ?? throw new BusinessException(404, "Không tìm thấy hồ sơ hội viên.");
    }

    private async Task<Coach> ResolveActiveCoachAsync(int coachId, CancellationToken ct) =>
        await db.Coaches
            .AsNoTracking()
            .Include(c => c.User)
            .SingleOrDefaultAsync(c => c.Id == coachId && c.User.IsActive && c.User.Role == UserRoles.Coach, ct)
            ?? throw new BusinessException(404, "Không tìm thấy huấn luyện viên.");

    private async Task<bool> HasTrainedWithCoachAsync(int memberId, int coachId, CancellationToken ct)
    {
        var now = Now;
        var attendedClass = await db.ClassRegistrations.AnyAsync(r =>
            r.MemberId == memberId &&
            r.Status == "Completed" &&
            r.Session != null &&
            r.Session.CoachId == coachId &&
            r.Session.Status == "Completed" &&
            r.Session.EndsAt <= now &&
            db.Attendances.Any(a =>
                a.SessionId == r.SessionId &&
                a.MemberId == memberId &&
                (a.Status == "Present" || a.Status == "Late")), ct);

        if (attendedClass) return true;

        return await db.TrainingPlans.AnyAsync(
            tp => tp.MemberId == memberId && tp.CoachId == coachId, ct);
    }

    private async Task<T> ExecuteTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await action();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || IsSqlConflict(ex))
        {
            throw new BusinessException(409, "Dữ liệu vừa thay đổi bởi yêu cầu khác. Vui lòng thử lại.");
        }
    }

    private static bool IsSqlConflict(Exception ex)
    {
        for (var cur = ex; cur != null; cur = cur.InnerException)
            if (cur is PostgresException pg && pg.SqlState is "23505" or "40001") return true;
        return false;
    }

    private static void ValidateRequest(CoachReviewRequest request)
    {
        if (request.Rating is < 1 or > 5)
            throw new BusinessException(400, "Số sao đánh giá phải từ 1 đến 5.");
        if (request.Comment is { Length: > 2000 })
            throw new BusinessException(400, "Nhận xét tối đa 2000 ký tự.");
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100)
            throw new BusinessException(400, "Page từ 1 đến 100000; PageSize từ 1 đến 100.");
    }

    private static string? NormalizeComment(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

    private static CoachReviewDTO ToDto(CoachReview r, string memberName) =>
        new(r.Id, r.CoachId, r.MemberId, memberName, r.Rating, r.Comment, r.CreatedAt, r.UpdatedAt);
}