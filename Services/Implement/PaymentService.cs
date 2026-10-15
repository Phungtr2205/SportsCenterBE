using Microsoft.EntityFrameworkCore;
using PayOS.Exceptions;
using PayOS.Models.Webhooks;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Payments;
using SportsCenterAPI.DTOs.Response;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Models.DTOs.Payments;
using SportsCenterAPI.Services.Interface;
using System.Data;

namespace SportsCenterAPI.Services.Implement;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _context;
    private readonly IPayOSService _payOSService;

    public PaymentService(
        AppDbContext context,
        IPayOSService payOSService)
    {
        _context = context;
        _payOSService = payOSService;
    }

    // Lấy danh sách thanh toán.
    // Member chỉ thấy giao dịch của chính mình.
    public async Task<ApiResponse<List<PaymentDTO>>> GetAllAsync(
        int actorId)
    {
        var actor = await GetActorAsync(actorId);

        var query = _context.Payments.AsNoTracking();

        if (actor.Role == "Member")
        {
            query = query.Where(p => p.Member.UserId == actorId);
        }

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync();

        return ApiResponse<List<PaymentDTO>>.Ok(
            payments.Select(MapToDTO).ToList(),
            "Lấy danh sách thanh toán thành công.");
    }

    public async Task<ApiResponse<PaymentDTO>> GetByIdAsync(
        int id,
        int actorId)
    {
        var payment = await GetOwnedPaymentAsync(id, actorId);

        return ApiResponse<PaymentDTO>.Ok(
            MapToDTO(payment),
            "Lấy thông tin thanh toán thành công.");
    }

    public async Task<ApiResponse<PaymentDTO>> CreateAsync(
        CreatePaymentDTO dto,
        int actorId)
    {
        if (dto == null || dto.SubscriptionId <= 0)
        {
            throw new BusinessException(
                400,
                "Vui lòng chọn đăng ký gói.");
        }

        Payment payment;

        // Lưu yêu cầu vào database trước khi gọi payOS.
        await using (var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable))
        {
            var actor = await GetActorAsync(actorId);

            var subscription = await _context.MemberSubscriptions
                .Include(s => s.Member)
                    .ThenInclude(m => m.User)
                .Include(s => s.Package)
                .SingleOrDefaultAsync(s => s.Id == dto.SubscriptionId);

            if (subscription == null)
            {
                throw new BusinessException(
                    404,
                    "Không tìm thấy đăng ký gói.");
            }

            CheckPermission(actor, subscription.Member.UserId);

            if (subscription.Status != "Pending")
            {
                throw new BusinessException(
                    409,
                    "Chỉ thanh toán đăng ký gói đang Pending.");
            }

            if (!subscription.Member.User.IsActive)
            {
                throw new BusinessException(
                    409,
                    "Tài khoản hội viên đã ngừng hoạt động.");
            }

            if (subscription.AgreedPrice <= 0 ||
                subscription.AgreedPrice > 9007199254740991m ||
                decimal.Truncate(subscription.AgreedPrice)
                    != subscription.AgreedPrice)
            {
                throw new BusinessException(
                    400,
                    "Giá thanh toán phải là số nguyên VND dương hợp lệ.");
            }

            if (subscription.DurationInDays is <= 0 or > 36500)
            {
                throw new BusinessException(
                    409,
                    "Thời hạn gói không hợp lệ.");
            }

            var hasBlockingPayment = await _context.Payments.AnyAsync(
                p => p.SubscriptionId == subscription.Id &&
                    (p.Status == "Completed" ||
                     p.Status == "NeedsReview"));

            if (hasBlockingPayment)
            {
                throw new BusinessException(
                    409,
                    "Đăng ký đã thanh toán hoặc có giao dịch cần đối soát.");
            }

            // Người dùng bấm lại thì dùng yêu cầu Pending đã có.
            var existingPayment = await _context.Payments
                .SingleOrDefaultAsync(
                    p => p.SubscriptionId == subscription.Id &&
                         p.PaymentMethod == "PayOS" &&
                         p.Status == "Pending");

            if (existingPayment != null)
            {
                payment = existingPayment;
            }
            else
            {
                // PostgreSQL sinh mã đơn duy nhất bằng sequence.
                var orderCodes = await _context.Database
                    .SqlQueryRaw<long>(
                        "SELECT nextval('public.\"PayOSOrderNumbers\"') AS \"Value\"")
                    .ToListAsync();

                var now = DateTime.UtcNow;

                payment = new Payment
                {
                    MemberId = subscription.MemberId,
                    SubscriptionId = subscription.Id,
                    Amount = subscription.AgreedPrice,
                    AmountReceived = 0,
                    PaymentMethod = "PayOS",
                    Status = "Pending",
                    CreatedAt = now,
                    PaymentDate = now,
                    PayOSOrderCode = orderCodes.Single(),
                    CreatedByUserId = actorId,
                    MemberNameSnapshot = subscription.Member.User.FullName,
                    PackageNameSnapshot = subscription.Package.PackageName
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                AddAudit(
                    payment.Id,
                    actorId,
                    "Create",
                    "Tạo yêu cầu thanh toán payOS.");

                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }

        // Gọi payOS sau khi đã lưu yêu cầu vào database.
        try
        {
            if (string.IsNullOrWhiteSpace(payment.PayOSPaymentLinkId))
            {
                await _payOSService.CreatePaymentLinkDetailsAsync(payment);
            }
        }
        catch (PayOSException ex)
        {
            // Ghi lỗi gốc để biết tại sao payOS không tạo được link.
            System.Diagnostics.Debug.WriteLine(
                $"Lỗi tạo link payOS, mã đơn {payment.PayOSOrderCode}: " +
                ex.Message);

            // Đối chiếu lại cùng mã đơn, tránh tạo thanh toán trùng.
            var recovered = await RefreshPaymentAsync(payment, actorId);

            return ApiResponse<PaymentDTO>.Ok(
                MapToDTO(recovered),
                "Đã đối chiếu lại yêu cầu thanh toán.");
        }

        var updated = await RefreshPaymentAsync(payment, actorId);

        return ApiResponse<PaymentDTO>.Ok(
            MapToDTO(updated),
            "Lấy thông tin thanh toán payOS thành công.");
    }

    public async Task PayOSWebhookAsync(Webhook dto)
    {
        // Bắt buộc xác minh trước khi sử dụng dữ liệu webhook.
        await _payOSService.VerifyWebhookAsync(dto);

        // Code trong Data nằm trong phần dữ liệu được ký.
        if (dto.Data.Code != "00")
        {
            return;
        }

        if (dto.Data.Currency != "VND" ||
            dto.Data.Amount <= 0 ||
            string.IsNullOrWhiteSpace(dto.Data.PaymentLinkId))
        {
            throw new BusinessException(
                400,
                "Dữ liệu giao dịch webhook không hợp lệ.");
        }

        var payment = await _context.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.PayOSOrderCode == dto.Data.OrderCode);

        // payOS có gửi dữ liệu mẫu khi đăng ký webhook.
        // Không tạo Payment từ webhook không có đơn tương ứng.
        if (payment == null)
        {
            return;
        }

        if (payment.PayOSPaymentLinkId != null &&
            payment.PayOSPaymentLinkId != dto.Data.PaymentLinkId)
        {
            throw new BusinessException(
                400,
                "Mã link thanh toán không khớp.");
        }

        // Webhook gửi lại không được kích hoạt gói lần thứ hai.
        if (payment.Status == "Completed")
        {
            return;
        }

        await RefreshPaymentAsync(
            payment,
            actorId: null,
            expectedLinkId: dto.Data.PaymentLinkId);
    }

    public async Task<ApiResponse<PaymentDTO>> SyncAsync(
        int id,
        int actorId)
    {
        var payment = await GetOwnedPaymentAsync(id, actorId);

        if (payment.PaymentMethod != "PayOS")
        {
            throw new BusinessException(
                400,
                "Giao dịch này không sử dụng payOS.");
        }

        if (payment.Status == "Completed")
        {
            return ApiResponse<PaymentDTO>.Ok(
                MapToDTO(payment),
                "Thanh toán đã hoàn tất.");
        }

        var updated = await RefreshPaymentAsync(payment, actorId);

        return ApiResponse<PaymentDTO>.Ok(
            MapToDTO(updated),
            "Đồng bộ trạng thái thanh toán thành công.");
    }

    public async Task<ApiResponse<PaymentDTO>> CancelAsync(
        int id,
        string reason,
        int actorId)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 200)
        {
            throw new BusinessException(
                400,
                "Lý do hủy phải từ 1 đến 200 ký tự.");
        }

        var payment = await GetOwnedPaymentAsync(id, actorId);

        if (payment.PaymentMethod != "PayOS" ||
            payment.Status != "Pending")
        {
            throw new BusinessException(
                409,
                "Chỉ hủy link payOS đang Pending.");
        }

        Payment updated;

        try
        {
            var result = await _payOSService.CancelPaymentLinkAsync(
                payment,
                reason.Trim());

            updated = await ApplyGatewayResultAsync(
                payment,
                result,
                actorId);
        }
        catch (PayOSException)
        {
            // Nếu yêu cầu hủy bị timeout, tra cứu lại trạng thái thực tế.
            updated = await RefreshPaymentAsync(payment, actorId);
        }

        return ApiResponse<PaymentDTO>.Ok(
            MapToDTO(updated),
            "Đã đối chiếu kết quả yêu cầu hủy.");
    }

    private async Task<Payment> RefreshPaymentAsync(
        Payment payment,
        int? actorId,
        string? expectedLinkId = null)
    {
        PayOSPaymentLinkResult result;

        try
        {
            // PayOSService hiện cập nhật GatewayStatus và
            // AmountReceived vào đối tượng payment được truyền vào.
            result = await _payOSService.GetPaymentLinkDetailsAsync(payment);
        }
        catch (PayOSException)
        {
            throw new BusinessException(
                503,
                "Chưa lấy được trạng thái payOS. Thử đồng bộ lại giao dịch này.");
        }

        if (expectedLinkId != null &&
            result.PaymentLinkId != expectedLinkId)
        {
            throw new BusinessException(
                400,
                "Mã link webhook không khớp kết quả payOS.");
        }

        return await ApplyGatewayResultAsync(payment, result, actorId);
    }

    private async Task<Payment> ApplyGatewayResultAsync(
        Payment source,
        PayOSPaymentLinkResult result,
        int? actorId)
    {
        var paymentId = source.Id;
        var orderCode = source.PayOSOrderCode;
        var gatewayStatus = source.GatewayStatus ?? string.Empty;
        var amountReceived = source.AmountReceived;

        // Đọc lại bản ghi để không ghi đè kết quả webhook vừa cập nhật.
        _context.ChangeTracker.Clear();

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var payment = await _context.Payments
            .Include(p => p.Subscription)
            .Include(p => p.Member)
                .ThenInclude(m => m.User)
            .SingleAsync(p => p.Id == paymentId);

        if (payment.Status == "Completed")
        {
            return payment;
        }

        if (payment.PayOSOrderCode != orderCode ||
            string.IsNullOrWhiteSpace(result.PaymentLinkId) ||
            (payment.PayOSPaymentLinkId != null &&
             payment.PayOSPaymentLinkId != result.PaymentLinkId))
        {
            throw new BusinessException(
                502,
                "Kết quả payOS không khớp thanh toán.");
        }

        // Bỏ qua response cũ có số tiền nhận thấp hơn bản đã lưu.
        if (amountReceived < payment.AmountReceived)
        {
            return payment;
        }

        var oldStatus = payment.Status;

        payment.PayOSPaymentLinkId = result.PaymentLinkId;
        payment.CheckoutUrl = result.PaymentUrl;

        if (!string.IsNullOrWhiteSpace(result.QrCode))
        {
            payment.QrCode = result.QrCode;
        }

        payment.GatewayStatus = gatewayStatus;
        payment.AmountReceived = amountReceived;

        switch (gatewayStatus.ToUpperInvariant())
        {
            case "PAID":
                await CompletePaymentAsync(payment, actorId);
                break;

            case "CANCELLED":
            case "EXPIRED":
            case "FAILED":
                if (amountReceived > 0)
                {
                    MarkForReview(payment);
                }
                else if (payment.Status == "Pending")
                {
                    payment.Status = gatewayStatus.ToUpperInvariant() switch
                    {
                        "CANCELLED" => "Cancelled",
                        "EXPIRED" => "Expired",
                        _ => "Failed"
                    };
                }
                break;

            default:
                if (amountReceived > 0)
                {
                    MarkForReview(payment);
                }
                break;
        }

        if (payment.Status != oldStatus)
        {
            AddAudit(
                payment.Id,
                actorId,
                "Update",
                $"{oldStatus} -> {payment.Status}; payOS: {gatewayStatus}.");
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return payment;
    }

    private async Task CompletePaymentAsync(
        Payment payment,
        int? actorId)
    {
        var subscription = payment.Subscription;

        var hasAnotherPayment = await _context.Payments.AnyAsync(
            p => p.Id != payment.Id &&
                 p.SubscriptionId == payment.SubscriptionId &&
                 (p.Status == "Completed" ||
                  p.Status == "Pending" ||
                  p.Status == "NeedsReview"));

        if (payment.AmountReceived != payment.Amount ||
            subscription == null ||
            subscription.Status != "Pending" ||
            subscription.AgreedPrice != payment.Amount ||
            subscription.DurationInDays is <= 0 or > 36500 ||
            !payment.Member.User.IsActive ||
            hasAnotherPayment)
        {
            MarkForReview(payment);
            return;
        }

        var now = DateTime.UtcNow;

        payment.Status = "Completed";
        payment.PaymentDate = now;
        payment.ReviewReason = null;
        payment.TransactionReference =
            $"INV-{now:yyyyMMdd}-{payment.Id:D8}";

        subscription.Status = "Active";
        subscription.StartDate = now;
        subscription.EndDate = now.AddDays(subscription.DurationInDays);

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = "Update",
            EntityName = nameof(MemberSubscription),
            EntityId = subscription.Id,
            Details = $"Kích hoạt gói từ thanh toán {payment.Id}.",
            Timestamp = now
        });

        _context.Notifications.Add(new Notification
        {
            UserId = payment.Member.UserId,
            Title = "Thanh toán thành công",
            Content = $"Gói tập đã được kích hoạt. Biên lai: {payment.TransactionReference}.",
            SentAt = now
        });
    }

    private static void MarkForReview(Payment payment)
    {
        payment.Status = "NeedsReview";
        payment.ReviewReason =
            "Có tiền chuyển đến nhưng số tiền hoặc điều kiện kích hoạt gói cần đối soát.";
    }

    private async Task<User> GetActorAsync(int actorId)
    {
        var actor = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == actorId && u.IsActive);

        if (actor == null)
        {
            throw new BusinessException(401, "Vui lòng đăng nhập.");
        }

        if (actor.Role is not ("Member" or "Receptionist" or "Manager"))
        {
            throw new BusinessException(
                403,
                "Không có quyền truy cập thanh toán.");
        }

        return actor;
    }

    private static void CheckPermission(User actor, int ownerUserId)
    {
        if (actor.Role is "Receptionist" or "Manager")
        {
            return;
        }

        if (actor.Id != ownerUserId)
        {
            throw new BusinessException(
                403,
                "Không có quyền truy cập thanh toán này.");
        }
    }

    private async Task<Payment> GetOwnedPaymentAsync(
        int id,
        int actorId)
    {
        var actor = await GetActorAsync(actorId);

        var payment = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Member)
            .SingleOrDefaultAsync(p => p.Id == id);

        if (payment == null)
        {
            throw new BusinessException(
                404,
                "Không tìm thấy thanh toán.");
        }

        CheckPermission(actor, payment.Member.UserId);

        return payment;
    }

    private void AddAudit(
        int paymentId,
        int? actorId,
        string action,
        string details)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = action,
            EntityName = nameof(Payment),
            EntityId = paymentId,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
    }

    private static PaymentDTO MapToDTO(Payment payment)
    {
        return new PaymentDTO
        {
            Id = payment.Id,
            MemberId = payment.MemberId,
            SubscriptionId = payment.SubscriptionId,
            Amount = payment.Amount,
            AmountReceived = payment.AmountReceived,
            PaymentMethod = payment.PaymentMethod,
            Status = payment.Status,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.Status == "Completed"
                ? payment.PaymentDate
                : null,
            TransactionReference = payment.TransactionReference,
            PayOSOrderCode = payment.PayOSOrderCode,
            PayOSPaymentLinkId = payment.PayOSPaymentLinkId,
            GatewayStatus = payment.GatewayStatus,
            CheckoutUrl = payment.CheckoutUrl,
            QrCode = payment.QrCode,
            ExpiresAt = payment.ExpiresAt,
            ReviewReason = payment.ReviewReason
        };
    }
   

}