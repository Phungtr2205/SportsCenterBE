using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Attendances;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class AttendanceService(AppDbContext context) : IAttendanceService
{
    public async Task<AttendanceResponseDTO> MarkAttendanceAsync(
        int actorId,
        MarkAttendanceDTO request,
        CancellationToken ct = default)
    {
        if (request.MemberId <= 0 || request.SessionId <= 0)
        {
            throw new BusinessException(
                400, "MemberId và SessionId phải lớn hơn 0.");
        }

        bool validStatus =
            request.Status == "Present" ||
            request.Status == "Absent" ||
            request.Status == "Late" ||
            request.Status == "Excused";

        if (!validStatus)
        {
            throw new BusinessException(
                400,
                "Status phải là Present, Absent, Late hoặc Excused.");
        }

        var session = await GetSessionForActorAsync(
            actorId,
            request.SessionId,
            allowDesk: true);

        if (session.Status == "Cancelled")
        {
            throw new BusinessException(
                409, "Không được điểm danh buổi học đã hủy.");
        }

        var registration = await context.ClassRegistrations
            .Include(r => r.Member)
                .ThenInclude(m => m.User)
            .Include(r => r.Class)
            .SingleOrDefaultAsync(r =>
                r.MemberId == request.MemberId &&
                r.SessionId == request.SessionId &&
                r.ClassId == session.ClassId &&
                r.Status != "Cancelled", ct);

        if (registration == null)
        {
            throw new BusinessException(
                409,
                "Hội viên chưa đăng ký buổi học này hoặc đã hủy đăng ký.");
        }

        var attendance = await context.Attendances
            .SingleOrDefaultAsync(a =>
                a.MemberId == request.MemberId &&
                a.SessionId == request.SessionId, ct);

        if (attendance == null)
        {
            attendance = new Attendance
            {
                MemberId = registration.MemberId,
                Member = registration.Member,
                ClassId = session.ClassId,
                SessionId = session.Id,
                Status = request.Status,
                CheckInTime = DateTime.UtcNow
            };

            context.Attendances.Add(attendance);
        }
        else
        {
            attendance.Status = request.Status;
            attendance.CheckInTime = DateTime.UtcNow;
            attendance.Member = registration.Member;
        }

        string statusLabel = request.Status switch
        {
            "Present" => "Có mặt",
            "Late" => "Đi trễ",
            "Absent" => "Vắng mặt",
            "Excused" => "Có phép",
            _ => request.Status
        };

        context.Notifications.Add(new Notification
        {
            UserId = registration.Member.UserId,
            Title = "Cập nhật điểm danh buổi học",
            Content = $"Buổi #{session.Id} - {registration.Class.ClassName} đã được cập nhật điểm danh: {statusLabel}.",
            SentAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(ct);

        return MapToDTO(attendance);
    }

    public async Task<IEnumerable<AttendanceResponseDTO>>
        GetSessionAttendancesAsync(int actorId, int sessionId, CancellationToken ct = default)
    {
        await GetSessionForActorAsync(
            actorId,
            sessionId,
            allowDesk: true);

        var attendances = await context.Attendances
            .AsNoTracking()
            .Include(a => a.Member)
                .ThenInclude(m => m.User)
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.Member.User.FullName)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

        return attendances.Select(MapToDTO).ToList();
    }

    public async Task<IEnumerable<AttendanceResponseDTO>>
        GetMyAttendancesAsync(int actorId, CancellationToken ct = default)
    {
        var member = await context.Members
            .AsNoTracking()
            .Include(m => m.User)
            .SingleOrDefaultAsync(m => m.UserId == actorId, ct)
            ?? throw new BusinessException(404, "Không tìm thấy hội viên.");

        var attendances = await context.Attendances
            .AsNoTracking()
            .Include(a => a.Member)
                .ThenInclude(m => m.User)
            .Where(a => a.MemberId == member.Id)
            .OrderByDescending(a => a.CheckInTime)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        return attendances.Select(MapToDTO).ToList();
    }

    private async Task<ClassSession> GetSessionForActorAsync(
        int actorId,
        int sessionId,
        bool allowDesk)
    {
        var actor = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u =>
                u.Id == actorId && u.IsActive);

        if (actor == null)
        {
            throw new BusinessException(
                401, "Tài khoản không hoạt động. Vui lòng đăng nhập lại.");
        }

        var session = await context.ClassSessions
            .AsNoTracking()
            .Include(s => s.Coach)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            throw new BusinessException(
                404, "Không tìm thấy buổi học.");
        }

        bool isAssignedCoach =
            actor.Role == UserRoles.Coach &&
            session.Coach.UserId == actorId;

        bool isDesk =
            actor.Role == UserRoles.Receptionist ||
            actor.Role == UserRoles.Manager;

        bool hasPermission =
            isAssignedCoach || (allowDesk && isDesk);

        if (!hasPermission)
        {
            throw new BusinessException(
                403, "Bạn không có quyền thao tác điểm danh buổi học này.");
        }

        return session;
    }

    private static AttendanceResponseDTO MapToDTO(
        Attendance attendance)
    {
        return new AttendanceResponseDTO
        {
            Id = attendance.Id,
            MemberId = attendance.MemberId,
            MemberName = attendance.Member.User.FullName,
            CheckInTime = attendance.CheckInTime,
            Status = attendance.Status,
            SessionId = attendance.SessionId
        };
    }
}