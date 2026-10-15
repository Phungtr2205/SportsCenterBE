using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Attendances;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class AttendanceService(AppDbContext context) : IAttendanceService
{
    /// <summary>
    /// Tạo hoặc cập nhật điểm danh.
    /// Chỉ HLV phụ trách buổi học được thực hiện.
    /// Hội viên phải đăng ký đúng buổi và chưa hủy.
    /// </summary>
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

        // Kiểm tra người thao tác có phải HLV phụ trách không.
        var session = await GetSessionForActorAsync(
            actorId,
            request.SessionId,
            allowDesk: false);

        if (session.Status == "Cancelled")
        {
            throw new BusinessException(
                409, "Không được điểm danh buổi học đã hủy.");
        }

        // Kiểm tra theo SessionId, tránh điểm danh nhầm buổi cùng lớp.
        var registration = await context.ClassRegistrations
            .Include(r => r.Member)
                .ThenInclude(m => m.User)
            .SingleOrDefaultAsync(r =>
                r.MemberId == request.MemberId &&
                r.SessionId == request.SessionId &&
                r.ClassId == session.ClassId &&
                r.Status != "Cancelled");

        if (registration == null)
        {
            throw new BusinessException(
                409,
                "Hội viên chưa đăng ký buổi học này hoặc đã hủy đăng ký.");
        }

        var attendance = await context.Attendances
            .SingleOrDefaultAsync(a =>
                a.MemberId == request.MemberId &&
                a.SessionId == request.SessionId);

        if (attendance == null)
        {
            // Chưa có thì tạo điểm danh mới.
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
            // Đã có thì sửa trạng thái, giữ thời gian ghi nhận ban đầu.
            attendance.Status = request.Status;
            attendance.Member = registration.Member;
        }

        await context.SaveChangesAsync();

        return MapToDTO(attendance);
    }

    /// <summary>
    /// Xem điểm danh theo buổi học.
    /// HLV chỉ xem buổi mình phụ trách.
    /// Lễ tân và Manager được xem danh sách.
    /// </summary>
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
            .ToListAsync();

        return attendances.Select(MapToDTO).ToList();
    }

    // Kiểm tra tài khoản và quyền trên một buổi học.
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