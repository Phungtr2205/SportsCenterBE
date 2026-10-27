using SportsCenterAPI.DTOs.Classes;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Implement;

namespace SportsCenterAPI.Tests;

public class ClassServiceTests
{
    [Fact]
    public async Task CreateClass_WithoutPrice_SetsPriceToZeroAndSucceeds()
    {
        // 1. Arrange: Chuẩn bị Manager, Bộ môn và Huấn luyện viên
        using var db = TestDbHelper.CreateDbContext();

        var manager = new User { Id = 10, Email = "manager@test.com", FullName = "Manager", Role = "Manager", PasswordHash = "h", IsActive = true };
        var sport = new Sport { Id = 1, Name = "Yoga", Description = "Yoga flow", IsActive = true };
        var coachUser = new User { Id = 20, Email = "coach@test.com", FullName = "Coach One", Role = "Coach", PasswordHash = "h", IsActive = true };
        var coach = new Coach { Id = 1, UserId = 20, User = coachUser, Specialization = "Yoga" };

        db.Users.AddRange(manager, coachUser);
        db.Sports.Add(sport);
        db.Coaches.Add(coach);
        await db.SaveChangesAsync();

        var service = new ClassService(db, TimeProvider.System);

        var now = DateTimeOffset.UtcNow;
        var request = new ClassRequest
        {
            ClassName = "Lop Yoga Co Ban",
            SportId = 1,
            CoachId = 1,
            MaxCapacity = 15,
            Schedule = "Thu 2, 4, 6",
            StartDate = now.AddDays(1),
            EndDate = now.AddDays(30),
            IsActive = true
            // Lưu ý: Không cần truyền Price nữa!
        };

        // 2. Act: Tạo lớp học mới (truyền id = null)
        var createdClass = await service.SaveClassAsync(10, null, request);

        // 3. Assert: Lớp học được tạo và Price tự động được gán = 0
        Assert.NotNull(createdClass);
        Assert.True(createdClass.Id > 0);
        Assert.Equal("Lop Yoga Co Ban", createdClass.ClassName);
        Assert.Equal(0, createdClass.Price); // Đảm bảo học phí tự động = 0
    }

    [Fact]
    public async Task CreateClass_StartDateAfterEndDate_ThrowsBadRequest()
    {
        using var db = TestDbHelper.CreateDbContext();
        var manager = new User { Id = 11, Email = "mgr2@test.com", FullName = "Manager 2", Role = "Manager", PasswordHash = "h", IsActive = true };
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        var service = new ClassService(db, TimeProvider.System);

        var now = DateTimeOffset.UtcNow;
        var request = new ClassRequest
        {
            ClassName = "Lop Loi Thoi Gian",
            SportId = 1,
            CoachId = 1,
            MaxCapacity = 10,
            StartDate = now.AddDays(10),
            EndDate = now.AddDays(2), // Ngày bắt đầu sau ngày kết thúc -> Không hợp lệ
            IsActive = true
        };

        // Báo lỗi 400 nếu thời gian không hợp lệ
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SaveClassAsync(11, null, request));

        Assert.Equal(400, ex.StatusCode);
    }
}