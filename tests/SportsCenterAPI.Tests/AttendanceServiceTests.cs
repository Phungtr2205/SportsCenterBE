using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.DTOs.Attendances;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Implement;

namespace SportsCenterAPI.Tests;

public class AttendanceServiceTests
{
    private static (AttendanceService service, Data.AppDbContext db) CreateService()
    {
        var db = TestDbHelper.CreateDbContext();
        var service = new AttendanceService(db);
        return (service, db);
    }

    [Fact]
    public async Task MarkAttendance_ValidPresent_CreatesAttendanceAndNotification()
    {
        // Arrange
        var (service, db) = CreateService();

        var coachUser = new User { Id = 1, FullName = "Coach John", Email = "coach@test.com", Role = UserRoles.Coach, IsActive = true };
        var coach = new Coach { Id = 1, UserId = 1, User = coachUser, Specialization = "Fitness" };

        var memberUser = new User { Id = 2, FullName = "Member Alice", Email = "alice@test.com", Role = UserRoles.Member, IsActive = true };
        var member = new Member { Id = 1, UserId = 2, User = memberUser };

        var sportClass = new SportClass { Id = 1, ClassName = "Morning Yoga", Price = 0 };
        var session = new ClassSession
        {
            Id = 1,
            ClassId = 1,
            Class = sportClass,
            CoachId = 1,
            Coach = coach,
            Status = "Scheduled",
            StartsAt = DateTime.UtcNow.AddHours(1),
            EndsAt = DateTime.UtcNow.AddHours(2)
        };

        var registration = new ClassRegistration
        {
            Id = 1,
            MemberId = 1,
            Member = member,
            ClassId = 1,
            Class = sportClass,
            SessionId = 1,
            Status = "Registered"
        };

        db.Users.AddRange(coachUser, memberUser);
        db.Coaches.Add(coach);
        db.Members.Add(member);
        db.SportClasses.Add(sportClass);
        db.ClassSessions.Add(session);
        db.ClassRegistrations.Add(registration);
        await db.SaveChangesAsync();

        var request = new MarkAttendanceDTO
        {
            MemberId = 1,
            SessionId = 1,
            Status = "Present"
        };

        // Act (actorId = 1 là HLV phụ trách)
        var result = await service.MarkAttendanceAsync(1, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Present", result.Status);
        Assert.Equal(1, result.MemberId);
        Assert.Equal("Member Alice", result.MemberName);

        var savedAttendance = await db.Attendances.FirstOrDefaultAsync(a => a.MemberId == 1 && a.SessionId == 1);
        Assert.NotNull(savedAttendance);
        Assert.Equal("Present", savedAttendance.Status);

        // Kiểm tra tự động gửi Notification cho học viên
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == 2);
        Assert.NotNull(notification);
        Assert.Contains("Có mặt", notification.Content);
    }

    [Fact]
    public async Task MarkAttendance_SessionCancelled_ThrowsConflict409()
    {
        // Arrange
        var (service, db) = CreateService();

        var coachUser = new User { Id = 1, FullName = "Coach John", Email = "coach@test.com", Role = UserRoles.Coach, IsActive = true };
        var coach = new Coach { Id = 1, UserId = 1, User = coachUser, Specialization = "Fitness" };
        var sportClass = new SportClass { Id = 1, ClassName = "Yoga", Price = 0 };
        var session = new ClassSession
        {
            Id = 1,
            ClassId = 1,
            Class = sportClass,
            CoachId = 1,
            Coach = coach,
            Status = "Cancelled",
            StartsAt = DateTime.UtcNow.AddHours(1),
            EndsAt = DateTime.UtcNow.AddHours(2)
        };

        db.Users.Add(coachUser);
        db.Coaches.Add(coach);
        db.SportClasses.Add(sportClass);
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        var request = new MarkAttendanceDTO { MemberId = 1, SessionId = 1, Status = "Present" };

        // Act & Assert (Không được điểm danh buổi học đã hủy)
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.MarkAttendanceAsync(1, request));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("Không được điểm danh buổi học đã hủy", ex.Message);
    }

    [Fact]
    public async Task MarkAttendance_UnregisteredMember_ThrowsConflict409()
    {
        // Arrange
        var (service, db) = CreateService();

        var coachUser = new User { Id = 1, FullName = "Coach John", Email = "coach@test.com", Role = UserRoles.Coach, IsActive = true };
        var coach = new Coach { Id = 1, UserId = 1, User = coachUser, Specialization = "Fitness" };
        var sportClass = new SportClass { Id = 1, ClassName = "Yoga", Price = 0 };
        var session = new ClassSession
        {
            Id = 1,
            ClassId = 1,
            Class = sportClass,
            CoachId = 1,
            Coach = coach,
            Status = "Scheduled",
            StartsAt = DateTime.UtcNow.AddHours(1),
            EndsAt = DateTime.UtcNow.AddHours(2)
        };

        db.Users.Add(coachUser);
        db.Coaches.Add(coach);
        db.SportClasses.Add(sportClass);
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        var request = new MarkAttendanceDTO { MemberId = 999, SessionId = 1, Status = "Present" };

        // Act & Assert (Hội viên chưa đăng ký buổi học)
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.MarkAttendanceAsync(1, request));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("chưa đăng ký buổi học", ex.Message);
    }

    [Fact]
    public async Task MarkAttendance_UnauthorizedActor_ThrowsForbidden403()
    {
        // Arrange
        var (service, db) = CreateService();

        // Coach 1 được phân công buổi học
        var coachUser1 = new User { Id = 1, FullName = "Coach John", Email = "coach1@test.com", Role = UserRoles.Coach, IsActive = true };
        var coach1 = new Coach { Id = 1, UserId = 1, User = coachUser1, Specialization = "Fitness" };

        // Coach 2 không liên quan
        var coachUser2 = new User { Id = 2, FullName = "Coach Jane", Email = "coach2@test.com", Role = UserRoles.Coach, IsActive = true };
        var coach2 = new Coach { Id = 2, UserId = 2, User = coachUser2, Specialization = "Fitness" };

        var sportClass = new SportClass { Id = 1, ClassName = "Yoga", Price = 0 };
        var session = new ClassSession
        {
            Id = 1,
            ClassId = 1,
            Class = sportClass,
            CoachId = 1,
            Coach = coach1,
            Status = "Scheduled",
            StartsAt = DateTime.UtcNow.AddHours(1),
            EndsAt = DateTime.UtcNow.AddHours(2)
        };

        db.Users.AddRange(coachUser1, coachUser2);
        db.Coaches.AddRange(coach1, coach2);
        db.SportClasses.Add(sportClass);
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        var request = new MarkAttendanceDTO { MemberId = 1, SessionId = 1, Status = "Present" };

        // Act & Assert (ActorId = 2 là Coach 2 không có quyền)
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.MarkAttendanceAsync(2, request));
        Assert.Equal(403, ex.StatusCode);
        Assert.Contains("không có quyền", ex.Message);
    }

    [Fact]
    public async Task MarkAttendance_InvalidStatus_ThrowsBadRequest400()
    {
        // Arrange
        var (service, _) = CreateService();
        var request = new MarkAttendanceDTO { MemberId = 1, SessionId = 1, Status = "UnknownStatus" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.MarkAttendanceAsync(1, request));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("Status phải là Present, Absent, Late hoặc Excused", ex.Message);
    }

    [Fact]
    public async Task GetMyAttendances_ExistingMember_ReturnsAttendancesList()
    {
        // Arrange
        var (service, db) = CreateService();

        var memberUser = new User { Id = 10, FullName = "Member Bob", Email = "bob@test.com", Role = UserRoles.Member, IsActive = true };
        var member = new Member { Id = 5, UserId = 10, User = memberUser };
        var sportClass = new SportClass { Id = 1, ClassName = "Gym Basic", Price = 0 };

        var attendance1 = new Attendance
        {
            Id = 1,
            MemberId = 5,
            Member = member,
            ClassId = 1,
            Class = sportClass,
            Status = "Present",
            CheckInTime = DateTime.UtcNow.AddDays(-1)
        };

        var attendance2 = new Attendance
        {
            Id = 2,
            MemberId = 5,
            Member = member,
            ClassId = 1,
            Class = sportClass,
            Status = "Late",
            CheckInTime = DateTime.UtcNow
        };

        db.Users.Add(memberUser);
        db.Members.Add(member);
        db.SportClasses.Add(sportClass);
        db.Attendances.AddRange(attendance1, attendance2);
        await db.SaveChangesAsync();

        // Act (actorId = 10 là UserId của member)
        var result = (await service.GetMyAttendancesAsync(10)).ToList();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Late", result[0].Status); // sắp xếp theo thời gian mới nhất lên đầu
        Assert.Equal("Present", result[1].Status);
    }
}