using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;
using SportsCenterAPI.Settings;

namespace SportsCenterAPI.Services.Implement;

public class PayOSService : IPayOSService
{
    private readonly PayOSClient _payOS;
    private readonly PayOSConfig _config;

    public PayOSService(IOptions<PayOSConfig> options)
    {
        _config = options.Value;

        _payOS = new PayOSClient(
            _config.ClientId,
            _config.ApiKey,
            _config.ChecksumKey);
    }

    public async Task<string> CreatePaymentLinkAsync(Payment payment)
    {
        var result = await CreatePaymentLinkDetailsAsync(payment);

        return result.PaymentUrl;
    }

    public async Task<PayOSPaymentLinkResult> CreatePaymentLinkDetailsAsync(
        Payment payment)
    {
        var orderCode = GetOrderCode(payment);

        if (payment.Amount <= 0 ||
            payment.Amount > 9007199254740991m ||
            decimal.Truncate(payment.Amount) != payment.Amount)
        {
            throw new InvalidOperationException("Số tiền thanh toán phải là số nguyên VND dương trong giới hạn cho phép.");
        }

        var request = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = decimal.ToInt64(payment.Amount),
            Description = $"SUB-{payment.Id}",
            ReturnUrl = _config.ReturnUrl,
            CancelUrl = _config.CancelUrl
        };

        var result = await _payOS.PaymentRequests.CreateAsync(request);

        payment.PayOSPaymentLinkId = result.PaymentLinkId;
        payment.CheckoutUrl = result.CheckoutUrl;
        payment.QrCode = result.QrCode;
        payment.GatewayStatus = result.Status.ToString();

        return new PayOSPaymentLinkResult
        {
            PaymentUrl = result.CheckoutUrl,
            QrCode = result.QrCode,
            PaymentLinkId = result.PaymentLinkId
        };
    }

    public async Task<PayOSPaymentLinkResult> CancelPaymentLinkAsync(
        Payment payment,
        string reason)
    {
        var orderCode = GetOrderCode(payment);

        var result = await _payOS.PaymentRequests.CancelAsync(
            orderCode,
            reason);

        payment.GatewayStatus = result.Status.ToString();
        payment.AmountReceived = result.AmountPaid;

        return new PayOSPaymentLinkResult
        {
            PaymentUrl = $"https://pay.payos.vn/web/{result.Id}",
            QrCode = payment.QrCode ?? string.Empty,
            PaymentLinkId = result.Id
        };
    }

    public async Task<PayOSPaymentLinkResult> GetPaymentLinkDetailsAsync(
        Payment payment)
    {
        var orderCode = GetOrderCode(payment);

        var result = await _payOS.PaymentRequests.GetAsync(orderCode);

        payment.GatewayStatus = result.Status.ToString();
        payment.AmountReceived = result.AmountPaid;

        return new PayOSPaymentLinkResult
        {
            PaymentUrl = $"https://pay.payos.vn/web/{result.Id}",
            QrCode = payment.QrCode ?? string.Empty,
            PaymentLinkId = result.Id
        };
    }

    private static long GetOrderCode(Payment payment)
    {
        if (!payment.PayOSOrderCode.HasValue ||
            payment.PayOSOrderCode.Value <= 0)
        {
            throw new InvalidOperationException("Thanh toán chưa có mã đơn payOS hợp lệ.");
        }

        return payment.PayOSOrderCode.Value;
    }

    public async Task VerifyWebhookAsync(Webhook dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await _payOS.Webhooks.VerifyAsync(dto);
    }
}