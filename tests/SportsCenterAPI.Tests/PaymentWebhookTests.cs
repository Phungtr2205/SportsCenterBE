using Moq;
using PayOS.Models.Webhooks;
using SportsCenterAPI.Data;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Implement;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Tests;

// Cần SQLite vì thanh toán chạy trong transaction Serializable và đọc lại bản ghi từ DB.
public class PaymentWebhookTests : IDisposable
{
    private const decimal Price = 500_000m;
    private readonly SqliteTestDb _sqlite = new();
    private readonly AppDbContext _db;

    public PaymentWebhookTests()
    {
        _db = _sqlite.CreateContext();
        var user = new User { Id = 500, Email = "m@test.com", FullName = "Member", Role = "Member", PasswordHash = "h", IsActive = true };
        var member = new Member { Id = 500, UserId = 500, User = user };
        var package = new MembershipPackage { Id = 500, PackageName = "Gói tháng", Price = Price, DurationInDays = 30 };
        var subscription = new MemberSubscription { Id = 500, MemberId = 500, Member = member, PackageId = 500, Package = package, AgreedPrice = Price, DurationInDays = 30 };
        _db.Payments.Add(new Payment
        {
            Id = 500, Amount = Price, PaymentMethod = "PayOS", Status = "Pending", MemberId = 500, Member = member,
            SubscriptionId = 500, Subscription = subscription, PayOSOrderCode = 1001, PayOSPaymentLinkId = "link1"
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        _db.Dispose();
        _sqlite.Dispose();
    }

    // payOS báo đã thu `received` đồng cho đơn 1001.
    private PaymentService ServiceWhereGatewayReceived(decimal received)
    {
        var payOS = new Mock<IPayOSService>();
        payOS.Setup(s => s.GetPaymentLinkDetailsAsync(It.IsAny<Payment>()))
            .Callback<Payment>(p => { p.GatewayStatus = "PAID"; p.AmountReceived = received; })
            .ReturnsAsync(new PayOSPaymentLinkResult { PaymentLinkId = "link1" });
        return new PaymentService(_db, payOS.Object);
    }

    private static Webhook PaidWebhook() => new()
    {
        Code = "00",
        Data = new WebhookData { Code = "00", OrderCode = 1001, Amount = 500_000, Currency = "VND", PaymentLinkId = "link1" }
    };

    [Fact]
    public async Task Webhook_ExactAmount_CompletesPayment_AndActivatesSubscription()
    {
        await ServiceWhereGatewayReceived(Price).PayOSWebhookAsync(PaidWebhook());

        _db.ChangeTracker.Clear();
        Assert.Equal("Completed", _db.Payments.Single().Status);
        Assert.Equal("Active", _db.MemberSubscriptions.Single().Status);
    }

    [Fact]
    public async Task Webhook_WrongAmount_GoesToNeedsReview_AndDoesNotActivate()
    {
        await ServiceWhereGatewayReceived(Price - 1).PayOSWebhookAsync(PaidWebhook());

        _db.ChangeTracker.Clear();
        Assert.Equal("NeedsReview", _db.Payments.Single().Status);
        Assert.Equal("Pending", _db.MemberSubscriptions.Single().Status);
    }

    [Fact]
    public async Task Webhook_Replayed_DoesNotActivateTwice()
    {
        var service = ServiceWhereGatewayReceived(Price);
        await service.PayOSWebhookAsync(PaidWebhook());
        _db.ChangeTracker.Clear();
        var firstEnd = _db.MemberSubscriptions.Single().EndDate;

        await service.PayOSWebhookAsync(PaidWebhook());

        _db.ChangeTracker.Clear();
        Assert.Equal(firstEnd, _db.MemberSubscriptions.Single().EndDate);
        Assert.Equal(1, _db.Payments.Count(p => p.Status == "Completed"));
    }
}
