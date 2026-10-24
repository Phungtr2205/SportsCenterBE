using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.DTOs.Classes;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;

namespace SportsCenterAPI.Services.Implement;

public partial class ClassService
{
    public async Task<ClassDTO> SaveClassAsync(int actorId, int? id, ClassRequest request, CancellationToken ct = default)
    {
        Validate(request);
        if (string.IsNullOrWhiteSpace(request.ClassName) || request.StartDate >= request.EndDate || request.EndDate.UtcDateTime <= Now ||
            request.StartDate.Year < 2000)
            throw new BusinessException(400, "Tên lớp, thời gian không hợp lệ.");
        var resultId = await Transaction(async () =>
        {
            await Manager(actorId, ct);
            if (!await db.Sports.AnyAsync(s => s.Id == request.SportId && s.IsActive, ct))
                throw new BusinessException(404, "Không tìm thấy bộ môn đang hoạt động.");
            await ActiveCoach(request.CoachId, ct);
            var item = id.HasValue ? await db.SportClasses.SingleOrDefaultAsync(c => c.Id == id, ct)
                ?? throw new BusinessException(404, "Không tìm thấy lớp học.") : new SportClass();
            if (id.HasValue)
            {
                if (!request.IsActive && await db.ClassSessions.AnyAsync(s => s.ClassId == id && s.Status == "Scheduled", ct))
                    throw new BusinessException(409, "Hãy hủy hoặc hoàn thành các buổi học trước khi đóng lớp.");
                if (await db.ClassSessions.AnyAsync(s => s.ClassId == id &&
                    (s.StartsAt < request.StartDate.UtcDateTime || s.EndsAt > request.EndDate.UtcDateTime), ct))
                    throw new BusinessException(409, "Khoảng thời gian lớp phải bao gồm mọi buổi học đã tạo.");
                if (item.SportId != request.SportId && await db.ClassRegistrations.AnyAsync(r => r.ClassId == id && r.Status != "Cancelled", ct))
                    throw new BusinessException(409, "Không đổi bộ môn khi đã có học viên đăng ký.");
            }
            item.ClassName = request.ClassName.Trim(); item.SportId = request.SportId; item.CoachId = request.CoachId;
            item.Price = 0; item.MaxCapacity = request.MaxCapacity; item.Schedule = request.Schedule?.Trim();
            item.StartDate = request.StartDate.UtcDateTime; item.EndDate = request.EndDate.UtcDateTime; item.IsActive = request.IsActive;
            if (!id.HasValue) db.SportClasses.Add(item);
            await db.SaveChangesAsync(ct);
            Audit(actorId, nameof(SportClass), item.Id, id.HasValue ? "Updated class" : "Created class");
            return item.Id;
        }, ct);
        return await GetClassAsync(resultId, ct);
    }

    public async Task DeactivateClassAsync(int actorId, int id, CancellationToken ct = default)
    {
        await Transaction(async () =>
        {
            await Manager(actorId, ct);
            var item = await db.SportClasses.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new BusinessException(404, "Không tìm thấy lớp học.");
            if (await db.ClassSessions.AnyAsync(s => s.ClassId == id && s.Status == "Scheduled", ct))
                throw new BusinessException(409, "Hãy hủy hoặc hoàn thành các buổi học trước khi đóng lớp.");
            item.IsActive = false;
            Audit(actorId, nameof(SportClass), id, "Deactivated class");
            return true;
        }, ct);
    }

    private async Task ActiveCoach(int coachId, CancellationToken ct)
    {
        if (!await db.Coaches.AnyAsync(c => c.Id == coachId && c.User.IsActive && c.User.Role == "Coach", ct))
            throw new BusinessException(404, "Không tìm thấy HLV đang hoạt động.");
    }

    public async Task<SessionDTO> SaveSessionAsync(int actorId, int classId, int? id, SessionRequest request, CancellationToken ct = default)
    {
        Validate(request);
        if (request.StartsAt >= request.EndsAt || request.StartsAt.UtcDateTime <= Now)
            throw new BusinessException(400, "Buổi học phải bắt đầu trong tương lai và kết thúc sau giờ bắt đầu.");
        var resultId = await Transaction(async () =>
        {
            await Manager(actorId, ct);
            var course = await db.SportClasses.Include(c => c.Sport).SingleOrDefaultAsync(c => c.Id == classId, ct)
                ?? throw new BusinessException(404, "Không tìm thấy lớp học.");
            if (!course.IsActive || !course.Sport.IsActive) throw new BusinessException(409, "Lớp hoặc bộ môn đã ngừng hoạt động.");
            if (request.StartsAt.UtcDateTime < course.StartDate || request.EndsAt.UtcDateTime > course.EndDate)
                throw new BusinessException(400, "Buổi học phải nằm trong khoảng thời gian của lớp.");
            await ActiveCoach(request.CoachId, ct);
            var policy = await db.CancellationPolicies.SingleOrDefaultAsync(p => p.Id == request.CancellationPolicyId && p.IsActive, ct)
                ?? throw new BusinessException(404, "Không tìm thấy chính sách hủy đang hoạt động.");
            var item = id.HasValue ? await db.ClassSessions.SingleOrDefaultAsync(s => s.Id == id && s.ClassId == classId, ct)
                ?? throw new BusinessException(404, "Không tìm thấy buổi học của lớp.") : new ClassSession { ClassId = classId };
            if (item.Status != "Scheduled" || (id.HasValue && item.StartsAt <= Now))
                throw new BusinessException(409, "Không sửa buổi học đã bắt đầu, hủy hoặc hoàn thành.");
            var enrolled = id.HasValue ? await db.ClassRegistrations.CountAsync(r => r.SessionId == id && r.Status != "Cancelled", ct) : 0;
            if (request.RoomId.HasValue && !await db.Rooms.AnyAsync(r => r.Id == request.RoomId.Value && r.IsActive, ct))
                throw new BusinessException(404, "Không tìm thấy phòng tập đang hoạt động.");
            if (request.RoomId.HasValue && await db.ClassSessions.AnyAsync(s => s.Id != (id ?? 0) && s.Status == "Scheduled" &&
                s.RoomId == request.RoomId.Value && s.StartsAt < request.EndsAt.UtcDateTime && s.EndsAt > request.StartsAt.UtcDateTime, ct))
                throw new BusinessException(409, "Phòng tập đã có buổi học khác trong khung giờ này.");
            if (request.Capacity < enrolled) throw new BusinessException(409, "Số chỗ không thể thấp hơn số học viên đã đăng ký.");
            if (await db.ClassSessions.AnyAsync(s => s.Id != (id ?? 0) && s.Status == "Scheduled" &&
                s.CoachId == request.CoachId && s.StartsAt < request.EndsAt.UtcDateTime && s.EndsAt > request.StartsAt.UtcDateTime, ct))
                throw new BusinessException(409, "HLV đã có buổi học khác trùng giờ.");

            bool scheduleOrCoachChanged = id.HasValue && enrolled > 0 && (
                item.StartsAt != request.StartsAt.UtcDateTime ||
                item.EndsAt != request.EndsAt.UtcDateTime ||
                item.CoachId != request.CoachId ||
                item.CancellationPolicyId != request.CancellationPolicyId ||
                item.RoomId != request.RoomId);

            item.CoachId = request.CoachId; item.CancellationPolicyId = request.CancellationPolicyId; item.RoomId = request.RoomId;
            item.StartsAt = request.StartsAt.UtcDateTime; item.EndsAt = request.EndsAt.UtcDateTime; item.Capacity = request.Capacity;
            if (!id.HasValue) db.ClassSessions.Add(item);
            await db.SaveChangesAsync(ct);

            if (scheduleOrCoachChanged)
            {
                var activeRegs = await db.ClassRegistrations
                    .Include(r => r.Member)
                    .Where(r => r.SessionId == item.Id && r.Status == "Registered")
                    .ToListAsync(ct);

                foreach (var reg in activeRegs)
                {
                    reg.CancellationDeadline = item.StartsAt.AddHours(-policy.MinimumHoursBeforeStart);
                    db.Notifications.Add(new Notification
                    {
                        UserId = reg.Member.UserId,
                        Title = "Cập nhật lịch buổi học",
                        Content = $"Buổi #{item.Id} - {course.ClassName} đã được cập nhật lịch học sang {item.StartsAt:dd/MM/yyyy HH:mm} (UTC). Vui lòng kiểm tra lại lịch của bạn.",
                        SentAt = Now
                    });
                }
            }

            Audit(actorId, nameof(ClassSession), item.Id, id.HasValue ? "Updated session" : "Created session");
            return item.Id;
        }, ct);
        return await SessionProjection(db.ClassSessions.AsNoTracking().Where(s => s.Id == resultId)).SingleAsync(ct);
    }

    public async Task CancelSessionAsync(int actorId, int id, CancelRequest request, CancellationToken ct = default)
    {
        Validate(request);
        await Transaction(async () =>
        {
            await Manager(actorId, ct);
            var session = await Session(id, ct);
            if (session.Status == "Cancelled") return true;
            if (session.Status != "Scheduled" || session.StartsAt <= Now)
                throw new BusinessException(409, "Chỉ hủy buổi học chưa bắt đầu.");
            session.Status = "Cancelled"; session.CancellationReason = request.Reason.Trim();
            var registrations = await db.ClassRegistrations.Include(r => r.Member).Where(r => r.SessionId == id && r.Status == "Registered").ToListAsync(ct);
            foreach (var registration in registrations)
            {
                registration.Status = "Cancelled"; registration.CancelledAt = Now;
                registration.CancelledByUserId = actorId; registration.CancellationReason = "Trung tâm hủy buổi: " + request.Reason.Trim();
                if (registration.CancellationReason.Length > 500) registration.CancellationReason = registration.CancellationReason[..500];
                db.Notifications.Add(new Notification { UserId = registration.Member.UserId, Title = "Buổi học đã hủy",
                    Content = $"Buổi {id} - {session.Class.ClassName} đã hủy. Lý do: {request.Reason.Trim()}", SentAt = Now });
            }
            await Notify(session, actorId, "Trung tâm hủy buổi học", $"Buổi {id} đã hủy; giải phóng {registrations.Count} chỗ.", true, ct);
            Audit(actorId, nameof(ClassSession), id, $"Cancelled session and {registrations.Count} registrations");
            return true;
        }, ct);
    }

    public async Task CompleteSessionAsync(int actorId, int id, CancellationToken ct = default)
    {
        await Transaction(async () =>
        {
            var session = await Session(id, ct);
            await EnsureSessionStaff(actorId, session, ct);
            if (session.Status == "Completed") return true;
            if (session.Status != "Scheduled" || session.EndsAt > Now)
                throw new BusinessException(409, "Chỉ hoàn thành buổi học đã kết thúc và chưa bị hủy.");
            session.Status = "Completed";
            var registrations = await db.ClassRegistrations.Where(r => r.SessionId == id && r.Status == "Registered").ToListAsync(ct);
            foreach (var registration in registrations) registration.Status = "Completed";
            Audit(actorId, nameof(ClassSession), id, "Completed session");
            return true;
        }, ct);
    }

    public async Task<List<CancellationPolicy>> GetPoliciesAsync(int actorId, CancellationToken ct = default)
    {
        await Manager(actorId, ct);
        return await db.CancellationPolicies.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct);
    }
    public async Task<CancellationPolicy> SavePolicyAsync(int actorId, int? id, PolicyRequest request, CancellationToken ct = default)
    {
        Validate(request);
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessException(400, "Tên chính sách không được để trống.");
        return await Transaction(async () =>
        {
            await Manager(actorId, ct);
            var policy = id.HasValue ? await db.CancellationPolicies.SingleOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new BusinessException(404, "Không tìm thấy chính sách.") : new CancellationPolicy();
            // Bookings retain their original cancellation deadline when a policy is edited.
            policy.Name = request.Name.Trim(); policy.MinimumHoursBeforeStart = request.MinimumHoursBeforeStart; policy.IsActive = request.IsActive;
            if (!id.HasValue) db.CancellationPolicies.Add(policy);
            await db.SaveChangesAsync(ct);
            Audit(actorId, nameof(CancellationPolicy), policy.Id, "Saved cancellation policy");
            return policy;
        }, ct);
    }
}
