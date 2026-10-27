using SportsCenterAPI.DTOs.Subscriptions;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Implement;

namespace SportsCenterAPI.Tests;

public class SubscriptionServiceTests
{
    [Fact]
    public async Task CreateAsync_NewMember_RegistersStandardPackageWithFullPrice()
    {
        using var db = TestDbHelper.CreateDbContext();
        var user = new User { Id = 101, Email = "mem1@test.com", FullName = "Member 1", Role = "Member", PasswordHash = "h", IsActive = true };
        var member = new Member { Id = 1, UserId = 101, User = user };
        var package = new MembershipPackage { Id = 1, PackageName = "Flex Pass 1 Thang", Price = 1000000m, DurationInDays = 30, IsActive = true };

        db.Users.Add(user);
        db.Members.Add(member);
        db.MembershipPackages.Add(package);
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);
        var request = new CreateSubscriptionRequestDTO { PackageId = 1 };

        // 1. Act: Đăng ký gói mới
        var result = await service.CreateAsync(101, null, request);

        // 2. Assert: Đăng ký thành công trạng thái Pending và đúng giá gốc
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(1000000m, result.AgreedPrice);
        Assert.Equal(30, result.DurationInDays);
    }

    [Fact]
    public async Task CreateAsync_ActiveStandardPackage_UpgradeToHigherPackage_CalculatesPriceDifference()
    {
        using var db = TestDbHelper.CreateDbContext();
        var user = new User { Id = 102, Email = "mem2@test.com", FullName = "Member 2", Role = "Member", PasswordHash = "h", IsActive = true };
        var member = new Member { Id = 2, UserId = 102, User = user };

        var pkg1M = new MembershipPackage { Id = 1, PackageName = "Flex Pass 1 Thang", Price = 1000000m, DurationInDays = 30, IsActive = true };
        var pkg6M = new MembershipPackage { Id = 2, PackageName = "Gold All-Access 6 Thang", Price = 4500000m, DurationInDays = 180, IsActive = true };

        var now = DateTime.UtcNow;
        var existingSub = new MemberSubscription
        {
            Id = 10,
            MemberId = 2,
            Package = pkg1M,
            AgreedPrice = 1000000m,
            DurationInDays = 30,
            Status = "Active",
            StartDate = now.AddDays(-5),
            EndDate = now.AddDays(25)
        };

        db.Users.Add(user);
        db.Members.Add(member);
        db.MembershipPackages.AddRange(pkg1M, pkg6M);
        db.MemberSubscriptions.Add(existingSub);
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);
        var request = new CreateSubscriptionRequestDTO { PackageId = 2 };

        // 1. Act: Nâng cấp lên gói 6 tháng
        var result = await service.CreateAsync(102, null, request);

        // 2. Assert: Tiền chốt phải là phần bù chênh lệch (4.500.000 - 1.000.000 = 3.500.000đ)
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(3500000m, result.AgreedPrice);
    }

    [Fact]
    public async Task CreateAsync_ActiveStandardPackage_UpgradeToLowerPackage_ThrowsConflict()
    {
        using var db = TestDbHelper.CreateDbContext();
        var user = new User { Id = 103, Email = "mem3@test.com", FullName = "Member 3", Role = "Member", PasswordHash = "h", IsActive = true };
        var member = new Member { Id = 3, UserId = 103, User = user };

        var pkg1M = new MembershipPackage { Id = 1, PackageName = "Flex Pass 1 Thang", Price = 1000000m, DurationInDays = 30, IsActive = true };
        var pkg6M = new MembershipPackage { Id = 2, PackageName = "Gold All-Access 6 Thang", Price = 4500000m, DurationInDays = 180, IsActive = true };

        var now = DateTime.UtcNow;
        var existingSub = new MemberSubscription
        {
            Id = 11,
            MemberId = 3,
            Package = pkg6M, // Đang dùng gói 4.500.000đ
            AgreedPrice = 4500000m,
            DurationInDays = 180,
            Status = "Active",
            StartDate = now.AddDays(-10),
            EndDate = now.AddDays(170)
        };

        db.Users.Add(user);
        db.Members.Add(member);
        db.MembershipPackages.AddRange(pkg1M, pkg6M);
        db.MemberSubscriptions.Add(existingSub);
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);
        var request = new CreateSubscriptionRequestDTO { PackageId = 1 }; // Muốn chọn gói 1.000.000đ thấp hơn

        // 1. Act & Assert: Phải chặn với mã lỗi 409
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(103, null, request));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_ActiveStandardPackage_RegisterParallelPtPackage_SucceedsWithFullPrice()
    {
        using var db = TestDbHelper.CreateDbContext();
        var user = new User { Id = 104, Email = "mem4@test.com", FullName = "Member 4", Role = "Member", PasswordHash = "h", IsActive = true };
        var member = new Member { Id = 4, UserId = 104, User = user };

        var pkgStandard = new MembershipPackage { Id = 1, PackageName = "Gold All-Access", Price = 4500000m, DurationInDays = 180, IsActive = true };
        var pkgPt = new MembershipPackage { Id = 3, PackageName = "PT Pro Training 1-1", Description = "Huấn luyện viên cá nhân", Price = 6000000m, DurationInDays = 90, IsActive = true };

        var now = DateTime.UtcNow;
        var existingSub = new MemberSubscription
        {
            Id = 12,
            MemberId = 4,
            Package = pkgStandard,
            AgreedPrice = 4500000m,
            DurationInDays = 180,
            Status = "Active",
            StartDate = now.AddDays(-10),
            EndDate = now.AddDays(170)
        };

        db.Users.Add(user);
        db.Members.Add(member);
        db.MembershipPackages.AddRange(pkgStandard, pkgPt);
        db.MemberSubscriptions.Add(existingSub);
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);
        var request = new CreateSubscriptionRequestDTO { PackageId = 3 }; // Mua gói PT

        // 1. Act: Đăng ký gói PT
        var result = await service.CreateAsync(104, null, request);

        // 2. Assert: Thành công, giữ nguyên 100% giá gói PT (6.000.000đ), không bị trừ chênh lệch của gói Standard
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(6000000m, result.AgreedPrice);
        Assert.Equal(90, result.DurationInDays);
    }

    [Fact]
    public async Task GetUpgradePreviewAsync_ValidUpgrade_ReturnsCorrectCalculation()
    {
        using var db = TestDbHelper.CreateDbContext();
        var user = new User { Id = 105, Email = "mem5@test.com", FullName = "Member 5", Role = "Member", PasswordHash = "h", IsActive = true };
        var member = new Member { Id = 5, UserId = 105, User = user };

        var pkg1M = new MembershipPackage { Id = 1, PackageName = "Flex Pass", Price = 1000000m, DurationInDays = 30, IsActive = true };
        var pkgVip = new MembershipPackage { Id = 4, PackageName = "Olympus VIP Club", Price = 12000000m, DurationInDays = 365, IsActive = true };

        var now = DateTime.UtcNow;
        var existingSub = new MemberSubscription
        {
            Id = 13,
            MemberId = 5,
            Package = pkg1M,
            AgreedPrice = 1000000m,
            DurationInDays = 30,
            Status = "Active",
            StartDate = now.AddDays(-3),
            EndDate = now.AddDays(27)
        };

        db.Users.Add(user);
        db.Members.Add(member);
        db.MembershipPackages.AddRange(pkg1M, pkgVip);
        db.MemberSubscriptions.Add(existingSub);
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);

        // 1. Act: Xem trước nâng cấp lên gói VIP
        var preview = await service.GetUpgradePreviewAsync(105, null, 4);

        // 2. Assert: Đủ điều kiện nâng cấp và số tiền bù trừ chính xác
        Assert.True(preview.CanUpgrade);
        Assert.Equal(1000000m, preview.DiscountAmount);
        Assert.Equal(11000000m, preview.AmountToPay); // 12.000.000 - 1.000.000 = 11.000.000đ
        Assert.Equal("Flex Pass", preview.CurrentPackageName);
        Assert.Equal("Olympus VIP Club", preview.TargetPackageName);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_UpgradePackage_SetsStartDateFromOldPackage_AndMarksOldPackageUpgraded()
    {
        using var db = TestDbHelper.CreateDbContext();
        var staff = new User { Id = 999, Email = "staff@test.com", FullName = "Staff", Role = "Receptionist", PasswordHash = "h", IsActive = true };
        var user = new User { Id = 106, Email = "mem6@test.com", FullName = "Member 6", Role = "Member", PasswordHash = "h", IsActive = true };
        var member = new Member { Id = 6, UserId = 106, User = user };

        var pkg1M = new MembershipPackage { Id = 1, PackageName = "Flex Pass", Price = 1000000m, DurationInDays = 30, IsActive = true };
        var pkg6M = new MembershipPackage { Id = 2, PackageName = "Gold All-Access", Price = 4500000m, DurationInDays = 180, IsActive = true };

        var originalStartDate = DateTime.UtcNow.AddDays(-5);
        var oldSub = new MemberSubscription
        {
            Id = 20,
            MemberId = 6,
            Package = pkg1M,
            AgreedPrice = 1000000m,
            DurationInDays = 30,
            Status = "Active",
            StartDate = originalStartDate,
            EndDate = originalStartDate.AddDays(30)
        };

        var newPendingSub = new MemberSubscription
        {
            Id = 21,
            MemberId = 6,
            Package = pkg6M,
            AgreedPrice = 3500000m, // Giá chênh lệch
            DurationInDays = 180,
            Status = "Pending"
        };

        db.Users.AddRange(staff, user);
        db.Members.Add(member);
        db.MembershipPackages.AddRange(pkg1M, pkg6M);
        db.MemberSubscriptions.AddRange(oldSub, newPendingSub);
        await db.SaveChangesAsync();

        var service = new SubscriptionService(db);
        var confirmRequest = new ConfirmPaymentRequestDTO
        {
            Amount = 3500000m,
            PaymentMethod = "Cash",
            PaymentReceived = true
        };

        // 1. Act: Lễ tân xác nhận thanh toán gói nâng cấp
        var receipt = await service.ConfirmPaymentAsync(999, 21, confirmRequest);

        // 2. Assert:
        Assert.NotNull(receipt);
        Assert.Equal("Completed", receipt.Status);

        // Gói mới: chuyển sang Active, kế thừa StartDate của gói cũ
        var updatedNewSub = await db.MemberSubscriptions.FindAsync(21);
        Assert.Equal("Active", updatedNewSub!.Status);
        Assert.Equal(originalStartDate, updatedNewSub.StartDate);
        Assert.Equal(originalStartDate.AddDays(180), updatedNewSub.EndDate);

        // Gói cũ: chuyển sang trạng thái Upgraded
        var updatedOldSub = await db.MemberSubscriptions.FindAsync(20);
        Assert.Equal("Upgraded", updatedOldSub!.Status);
    }
}