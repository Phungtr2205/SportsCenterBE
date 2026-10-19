using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq.Expressions;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Classes;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public partial class ClassService : IClassService
{
    // Giữ tên db và clock để các file partial dùng chung.
    private readonly AppDbContext db;
    private readonly TimeProvider clock;

    public ClassService(
        AppDbContext context,
        TimeProvider timeProvider)
    {
        db = context;
        clock = timeProvider;
    }

    private DateTime Now
    {
        get
        {
            return clock.GetUtcNow().UtcDateTime;
        }
    }

    // Kiểm tra tài khoản thuộc lễ tân hoặc quản lý.
    private static bool IsDesk(User user)
    {
        return user.Role == "Manager" ||
               user.Role == "Receptionist";
    }

    // Lấy tài khoản đang thực hiện thao tác.
    private async Task<User> Actor(
        int id,
        CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            u => u.Id == id && u.IsActive,
            ct);

        if (user == null)
        {
            throw new BusinessException(
                401,
                "Tài khoản không hoạt động. Vui lòng đăng nhập lại.");
        }

        return user;
    }

    // Kiểm tra quyền quản lý.
    private async Task<User> Manager(
        int id,
        CancellationToken ct)
    {
        var actor = await Actor(id, ct);

        if (actor.Role != "Manager")
        {
            throw new BusinessException(
                403,
                "Chỉ Manager được thực hiện thao tác này.");
        }

        return actor;
    }

    // Xác định hội viên cần xử lý.
    // Có memberId: nhân viên đang thao tác hộ.
    // Không có memberId: hội viên đang thao tác cho mình.
    private async Task<Member> ResolveMember(
        int actorId,
        int? memberId,
        CancellationToken ct)
    {
        var actor = await Actor(actorId, ct);

        if (memberId.HasValue && !IsDesk(actor))
        {
            throw new BusinessException(
                403,
                "Chỉ lễ tân hoặc Manager được chọn hội viên khác.");
        }

        if (!memberId.HasValue && actor.Role != "Member")
        {
            throw new BusinessException(
                403,
                "Nhân viên cần chọn hội viên cụ thể.");
        }

        var query = db.Members
            .Include(m => m.User)
            .AsQueryable();

        if (memberId.HasValue)
        {
            query = query.Where(m => m.Id == memberId.Value);
        }
        else
        {
            query = query.Where(m => m.UserId == actorId);
        }

        var member = await query.SingleOrDefaultAsync(ct);

        if (member == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy hội viên.");
        }

        return member;
    }

    // Lấy buổi học cùng lớp, bộ môn, HLV và chính sách hủy.
    private async Task<ClassSession> Session(
        int id,
        CancellationToken ct)
    {
        var session = await db.ClassSessions
            .Include(s => s.Class)
                .ThenInclude(c => c.Sport)
            .Include(s => s.Coach)
                .ThenInclude(c => c.User)
            .Include(s => s.CancellationPolicy)
            .SingleOrDefaultAsync(s => s.Id == id, ct);

        if (session == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy buổi học.");
        }

        return session;
    }

    // Cho phép lễ tân, quản lý hoặc đúng HLV phụ trách buổi học.
    private async Task EnsureSessionStaff(
        int actorId,
        ClassSession session,
        CancellationToken ct)
    {
        var actor = await Actor(actorId, ct);

        var isDesk = IsDesk(actor);

        var isAssignedCoach =
            actor.Role == "Coach" &&
            session.Coach.UserId == actorId;

        if (!isDesk && !isAssignedCoach)
        {
            throw new BusinessException(
                403,
                "Bạn không có quyền quản lý học viên của buổi học này.");
        }
    }

    // Kiểm tra các thuộc tính Required, Range, StringLength... trong DTO.
    private static void Validate(object request)
    {
        var errors = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            errors,
            validateAllProperties: true);

        if (!isValid)
        {
            var message = string.Join(
                " ",
                errors.Select(error => error.ErrorMessage));

            throw new BusinessException(400, message);
        }
    }

    private static void Pagination(int page, int size)
    {
        if (page < 1 || page > 100000 ||
            size < 1 || size > 100)
        {
            throw new BusinessException(
                400,
                "Page từ 1 đến 100000; PageSize từ 1 đến 100.");
        }
    }

    // Thêm nhật ký thao tác; Transaction sẽ lưu vào database.
    private void Audit(
        int actorId,
        string entity,
        int id,
        string details)
    {
        var audit = new AuditLog
        {
            UserId = actorId,
            Action = "Update",
            EntityName = entity,
            EntityId = id,
            Details = details,
            Timestamp = Now
        };

        db.AuditLogs.Add(audit);
    }

    // Tạo thông báo cho hội viên, HLV và quản lý nếu được yêu cầu.
    private async Task Notify(
        ClassSession session,
        int memberUserId,
        string title,
        string content,
        bool includeManagers,
        CancellationToken ct)
    {
        var recipients = new HashSet<int>
        {
            memberUserId,
            session.Coach.UserId
        };

        if (includeManagers)
        {
            var managerIds = await db.Users
                .Where(u => u.Role == "Manager" && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(ct);

            recipients.UnionWith(managerIds);
        }

        foreach (var userId in recipients)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Content = content,
                SentAt = Now
            };

            db.Notifications.Add(notification);
        }
    }

    // Thực hiện các thay đổi trong cùng một transaction.
    private async Task<T> Transaction<T>(
        Func<Task<T>> action,
        CancellationToken ct)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);

        try
        {
            var result = await action();

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return result;
        }
        catch (Exception ex) when (
            ex is DbUpdateConcurrencyException ||
            SqlConflict(ex))
        {
            throw new BusinessException(
                409,
                "Dữ liệu vừa thay đổi bởi yêu cầu khác. Vui lòng tải lại và thử lại.");
        }
    }

    // Nhận diện lỗi tranh chấp dữ liệu hoặc trùng khóa SQL Server.
    private static bool SqlConflict(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
            if (current is PostgresException pg && pg.SqlState is "23505" or "40001") return true;
        return false;
    }

    // Chọn các trường của lớp học để trả về DTO.
    // Dùng Expression để EF Core chuyển thành truy vấn SQL.
    private static readonly Expression<Func<SportClass, ClassDTO>>
        ClassProjection = c => new ClassDTO(
            c.Id,
            c.ClassName,
            c.SportId,
            c.Sport.Name,
            c.CoachId,
            c.Coach == null ? null : c.Coach.User.FullName,
            c.Price,
            c.MaxCapacity,
            c.Schedule,
            c.StartDate,
            c.EndDate,
            c.IsActive
        );

    // Chọn các trường của đăng ký lớp.
    // Chọn các trường của đăng ký lớp (kèm kết quả điểm danh và đánh giá).
    private Expression<Func<ClassRegistration, RegistrationDTO>>
        RegistrationProjection => r => new RegistrationDTO(
            r.Id,
            r.MemberId,
            r.ClassId,
            r.SessionId,
            r.Class.ClassName,
            r.Session == null ? null : r.Session.StartsAt,
            r.Session == null ? null : r.Session.EndsAt,
            r.Status,
            r.RegistrationDate,
            r.CreatedByUserId,
            r.CancellationDeadline,
            r.CancelledAt,
            r.CancelledByUserId,
            r.CancellationReason,
            r.Session == null ? null : r.Session.Status,
            db.Attendances
                .Where(a => a.SessionId == r.SessionId && a.MemberId == r.MemberId)
                .Select(a => a.Status)
                .FirstOrDefault(),
            db.Attendances
                .Where(a => a.SessionId == r.SessionId && a.MemberId == r.MemberId && (a.Status == "Present" || a.Status == "Late"))
                .Select(a => (DateTime?)a.CheckInTime)
                .FirstOrDefault(),
            db.ClassReviews.Any(rv => rv.RegistrationId == r.Id),
            db.ClassReviews
                .Where(rv => rv.RegistrationId == r.Id)
                .Select(rv => (int?)rv.Rating)
                .FirstOrDefault(),
            db.ClassReviews
                .Where(rv => rv.RegistrationId == r.Id)
                .Select(rv => new ReviewDTO(rv.Id, rv.RegistrationId, rv.Rating, rv.Comment, rv.CreatedAt))
                .FirstOrDefault()
        );

    private async Task<RegistrationDTO> RegistrationResult(
        int id,
        CancellationToken ct)
    {
        var registration = await db.ClassRegistrations
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(RegistrationProjection)
            .SingleAsync(ct);

        return registration;
    }

    // Tính số học viên và số chỗ còn lại của buổi học.
    private IQueryable<SessionDTO> SessionProjection(
        IQueryable<ClassSession> query)
    {
        var now = Now;

        return query.Select(s => new SessionDTO(
            s.Id,
            s.ClassId,
            s.Class.ClassName,
            s.CoachId,
            s.Coach.User.FullName,
            s.StartsAt,
            s.EndsAt,
            s.Capacity,

            // Số học viên chưa hủy đăng ký.
            s.Registrations.Count(r => r.Status != "Cancelled"),

            // Chỉ trả số chỗ còn lại khi buổi học còn nhận đăng ký.
            s.Status == "Scheduled" &&
            s.StartsAt > now &&
            s.Class.IsActive &&
            s.Class.Sport.IsActive &&
            s.Coach.User.IsActive &&
            s.Coach.User.Role == "Coach" &&
            s.CancellationPolicy.IsActive
                ? s.Capacity -
                  s.Registrations.Count(r => r.Status != "Cancelled")
                : 0,

            s.Status,
            s.CancellationPolicyId,
            s.CancellationPolicy.MinimumHoursBeforeStart
        ));
    }

    // Tìm lớp đang hoạt động cho người dùng.
    public async Task<PageResult<ClassDTO>> SearchClassesAsync(
        ClassSearch search,
        CancellationToken ct = default)
    {
        return await SearchClasses(
            search,
            includeInactive: false,
            ct: ct);
    }

    // Quản lý được xem cả lớp đã ngừng hoạt động.
    public async Task<PageResult<ClassDTO>> GetManagedClassesAsync(
        int actorId,
        ClassSearch search,
        CancellationToken ct = default)
    {
        await Manager(actorId, ct);

        return await SearchClasses(
            search,
            includeInactive: true,
            ct: ct);
    }

    private async Task<PageResult<ClassDTO>> SearchClasses(
        ClassSearch search,
        bool includeInactive,
        CancellationToken ct)
    {
        Validate(search);

        if (search.From >= search.To)
        {
            throw new BusinessException(
                400,
                "From phải trước To.");
        }

        var query = db.SportClasses
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(
                c => c.IsActive && c.Sport.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var keyword = search.Search.Trim();

            query = query.Where(
                c => c.ClassName.Contains(keyword));
        }

        if (search.SportId.HasValue)
        {
            query = query.Where(
                c => c.SportId == search.SportId.Value);
        }

        if (search.CoachId.HasValue)
        {
            query = query.Where(
                c => c.CoachId == search.CoachId.Value);
        }

        if (search.From.HasValue)
        {
            var from = search.From.Value.UtcDateTime;

            query = query.Where(c => c.EndDate > from);
        }

        if (search.To.HasValue)
        {
            var to = search.To.Value.UtcDateTime;

            query = query.Where(c => c.StartDate < to);
        }

        if (search.OnlyAvailable)
        {
            var now = Now;

            query = query.Where(c =>
                c.IsActive &&
                c.Sport.IsActive &&
                db.ClassSessions.Any(s =>
                    s.ClassId == c.Id &&
                    s.Status == "Scheduled" &&
                    s.StartsAt > now &&
                    s.Coach.User.IsActive &&
                    s.Coach.User.Role == "Coach" &&
                    s.CancellationPolicy.IsActive &&
                    s.Registrations.Count(
                        r => r.Status != "Cancelled") < s.Capacity));
        }

        var items = await query
            .OrderBy(c => c.Id)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(ClassProjection)
            .ToListAsync(ct);

        var total = await query.CountAsync(ct);

        return new PageResult<ClassDTO>(
            items,
            total,
            search.Page,
            search.PageSize);
    }

    // Lấy chi tiết một lớp học.
    public async Task<ClassDTO> GetClassAsync(
        int id,
        CancellationToken ct = default)
    {
        var item = await db.SportClasses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(ClassProjection)
            .SingleOrDefaultAsync(ct);

        if (item == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy lớp học.");
        }

        return item;
    }

    // Tìm buổi học theo lớp, HLV, bộ môn hoặc thời gian.
    public async Task<PageResult<SessionDTO>> SearchSessionsAsync(
        ClassSearch search,
        int? classId = null,
        int? actorCoachId = null,
        CancellationToken ct = default)
    {
        Validate(search);

        if (search.From >= search.To)
        {
            throw new BusinessException(
                400,
                "From phải trước To.");
        }

        var query = db.ClassSessions
            .AsNoTracking()
            .AsQueryable();

        if (actorCoachId.HasValue)
        {
            var actor = await Actor(actorCoachId.Value, ct);

            if (actor.Role != "Coach")
            {
                throw new BusinessException(
                    403,
                    "Chỉ dành cho HLV.");
            }

            // HLV chỉ xem lịch do mình phụ trách.
            query = query.Where(
                s => s.Coach.UserId == actor.Id);
        }
        else
        {
            query = query.Where(
                s => s.Class.IsActive && s.Class.Sport.IsActive);
        }

        if (classId.HasValue)
        {
            query = query.Where(
                s => s.ClassId == classId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var keyword = search.Search.Trim();

            query = query.Where(
                s => s.Class.ClassName.Contains(keyword));
        }

        if (search.SportId.HasValue)
        {
            query = query.Where(
                s => s.Class.SportId == search.SportId.Value);
        }

        if (search.CoachId.HasValue)
        {
            query = query.Where(
                s => s.CoachId == search.CoachId.Value);
        }

        if (search.From.HasValue)
        {
            var from = search.From.Value.UtcDateTime;

            query = query.Where(s => s.StartsAt >= from);
        }

        if (search.To.HasValue)
        {
            var to = search.To.Value.UtcDateTime;

            query = query.Where(s => s.StartsAt < to);
        }

        if (search.OnlyAvailable)
        {
            var now = Now;

            query = query.Where(s =>
                s.Status == "Scheduled" &&
                s.StartsAt > now &&
                s.Coach.User.IsActive &&
                s.Coach.User.Role == "Coach" &&
                s.CancellationPolicy.IsActive &&
                s.Registrations.Count(
                    r => r.Status != "Cancelled") < s.Capacity);
        }

        var pagedQuery = query
            .OrderBy(s => s.StartsAt)
            .ThenBy(s => s.Id)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize);

        var items = await SessionProjection(pagedQuery)
            .ToListAsync(ct);

        var total = await query.CountAsync(ct);

        return new PageResult<SessionDTO>(
            items,
            total,
            search.Page,
            search.PageSize);
    }
}