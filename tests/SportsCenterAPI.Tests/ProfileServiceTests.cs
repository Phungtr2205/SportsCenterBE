using Microsoft.Extensions.Caching.Memory;
using Moq;
using SportsCenterAPI.DTOs.Profile;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Implement;
using SportsCenterAPI.Services.Interface;
using System.Text.RegularExpressions;

namespace SportsCenterAPI.Tests;

public class ProfileServiceTests
{
    private readonly Mock<IEmailService> _mockEmail = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public async Task UpdateAsync_ValidData_UpdatesMemberFieldsSuccessfully()
    {
        // 1. Arrange: Chuẩn bị dữ liệu mẫu trong DB ảo
        using var db = TestDbHelper.CreateDbContext();
        var user = new User
        {
            Id = 1,
            Email = "member@test.com",
            FullName = "Nguyen Van A",
            Role = "Member",
            PasswordHash = "hash123",
            IsActive = true
        };
        var member = new Member
        {
            Id = 1,
            UserId = 1,
            User = user,
            Address = "Ha Noi",
            FitnessGoal = "Giam can"
        };
        db.Users.Add(user);
        db.Members.Add(member);
        await db.SaveChangesAsync();

        var service = new ProfileService(db, _mockEmail.Object, _cache);

        var updateDto = new UpdateProfileDTO
        {
            FullName = "Nguyen Van A (Cap nhat)",
            PhoneNumber = "0987654321",
            Gender = "Nam",
            Address = "TP Ho Chi Minh",
            FitnessGoal = "Tang co",
            DateOfBirth = new DateTime(2000, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            EmergencyContact = "0912345678"
        };

        // 2. Act: Thực thi hàm cập nhật hồ sơ
        var result = await service.UpdateAsync(1, updateDto);

        // 3. Assert: Kiểm tra dữ liệu được lưu đúng
        Assert.NotNull(result);
        Assert.Equal("Nguyen Van A (Cap nhat)", result.FullName);
        Assert.Equal("0987654321", result.PhoneNumber);
        Assert.Equal("Nam", result.Gender);
        Assert.Equal("TP Ho Chi Minh", result.Address);
        Assert.Equal("Tang co", result.FitnessGoal);
        Assert.Equal("0912345678", result.EmergencyContact);
    }

    [Fact]
    public async Task SendChangeEmailOtp_SameEmail_ThrowsBadRequest()
    {
        using var db = TestDbHelper.CreateDbContext();
        var user = new User { Id = 2, Email = "current@test.com", FullName = "B", Role = "Member", PasswordHash = "h", IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new ProfileService(db, _mockEmail.Object, _cache);

        // Đổi trùng email hiện tại phải ném ra lỗi 400
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SendChangeEmailOtpAsync(2, new RequestChangeEmailOtpDTO { NewEmail = "current@test.com" }));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task VerifyChangeEmailOtp_CorrectOtp_UpdatesEmailSuccessfully()
    {
        using var db = TestDbHelper.CreateDbContext();
        var user = new User { Id = 3, Email = "old@test.com", FullName = "C", Role = "Member", PasswordHash = "h", IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        string sentOtp = "";
        _mockEmail.Setup(e => e.SendEmailAsync("new@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Callback<string, string, string, string?>((_, _, body, _) =>
            {
                // Bắt mã OTP 6 số được sinh từ nội dung email
                sentOtp = Regex.Match(body, @"\b\d{6}\b").Value;
            })
            .Returns(Task.CompletedTask);

        var service = new ProfileService(db, _mockEmail.Object, _cache);

        // Gửi OTP
        await service.SendChangeEmailOtpAsync(3, new RequestChangeEmailOtpDTO { NewEmail = "new@test.com" });
        Assert.NotEmpty(sentOtp);

        // Xác thực với đúng email và đúng mã OTP
        var updatedProfile = await service.VerifyChangeEmailOtpAsync(3, new VerifyChangeEmailOtpDTO
        {
            NewEmail = "new@test.com",
            Otp = sentOtp
        });

        // Assert: Email tài khoản đã được đổi sang new@test.com
        Assert.Equal("new@test.com", updatedProfile.Email);
        var dbUser = await db.Users.FindAsync(3);
        Assert.Equal("new@test.com", dbUser!.Email);
    }

    [Fact]
    public async Task VerifyChangeEmailOtp_WrongOtp_ThrowsBadRequest()
    {
        using var db = TestDbHelper.CreateDbContext();
        var user = new User { Id = 4, Email = "test4@test.com", FullName = "D", Role = "Member", PasswordHash = "h", IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new ProfileService(db, _mockEmail.Object, _cache);
        await service.SendChangeEmailOtpAsync(4, new RequestChangeEmailOtpDTO { NewEmail = "another@test.com" });

        // Nhập đúng email nhưng sai mã OTP -> phải báo lỗi 400
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.VerifyChangeEmailOtpAsync(4, new VerifyChangeEmailOtpDTO
            {
                NewEmail = "another@test.com",
                Otp = "999999"
            }));

        Assert.Equal(400, ex.StatusCode);
    }
}