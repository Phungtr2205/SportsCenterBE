using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Response;
using SportsCenterAPI.DTOs.Subscriptions;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/member-subscriptions")]
[Authorize]
public class MemberSubscriptionsController(ISubscriptionService subscriptions) : ControllerBase
{
    /// <summary>
    /// Đăng ký gói tập và chờ thanh toán
    /// </summary>
    /// <remarks>Đăng ký ở trạng thái Pending và chỉ được kích hoạt sau khi thanh toán thành công.</remarks>
    [HttpPost]
    [Authorize(Roles = "Member")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionDTO>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateSubscriptionRequestDTO request, CancellationToken cancellationToken)
    {
        var result = await subscriptions.CreateAsync(ActorId(), null, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id },
            ApiResponse<SubscriptionDTO>.CreatedAt(result, "Subscription created; awaiting payment."));
    }

    /// <summary>
    /// Đăng ký gói tập thay cho hội viên
    /// </summary>
    [HttpPost("members/{memberId:int}")]
    [Authorize(Roles = "Receptionist,Manager")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionDTO>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateForMember(int memberId, CreateSubscriptionRequestDTO request, CancellationToken cancellationToken)
    {
        var result = await subscriptions.CreateAsync(ActorId(), memberId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id },
            ApiResponse<SubscriptionDTO>.CreatedAt(result, "Subscription created; awaiting payment."));
    }

    /// <summary>
    /// Xem các gói tập tôi đã đăng ký và thời hạn sử dụng
    /// </summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Member")]
    [ProducesResponseType(typeof(ApiResponse<List<SubscriptionDTO>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken) =>
        Ok(ApiResponse<List<SubscriptionDTO>>.Ok(
            await subscriptions.GetForMemberAsync(ActorId(), null, cancellationToken), "Subscriptions retrieved."));

    /// <summary>
    /// Xem các gói tập đã đăng ký của một hội viên
    /// </summary>
    [HttpGet("members/{memberId:int}")]
    [Authorize(Roles = "Receptionist,Manager")]
    [ProducesResponseType(typeof(ApiResponse<List<SubscriptionDTO>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForMember(int memberId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<List<SubscriptionDTO>>.Ok(
            await subscriptions.GetForMemberAsync(ActorId(), memberId, cancellationToken), "Subscriptions retrieved."));

    /// <summary>
    /// Xem chi tiết một đăng ký gói tập
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Member,Receptionist,Manager")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SubscriptionDTO>.Ok(
            await subscriptions.GetByIdAsync(ActorId(), id, cancellationToken), "Subscription retrieved."));

    /// <summary>
    /// Hủy đăng ký gói tập chưa thanh toán
    /// </summary>
    /// <remarks>Chỉ hủy đăng ký Pending chưa thanh toán. Gói đã thanh toán cần quy trình hoàn tiền riêng.</remarks>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "Member,Receptionist,Manager")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelPending(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SubscriptionDTO>.Ok(
            await subscriptions.CancelPendingAsync(ActorId(), id, cancellationToken), "Pending subscription cancelled."));

    /// <summary>
    /// Nhân viên xác nhận thanh toán và kích hoạt gói tập
    /// </summary>
    /// <remarks>API ghi nhận khoản thanh toán đã được kiểm tra và kích hoạt gói; không thực hiện trừ tiền ngân hàng hoặc thẻ.</remarks>
    [HttpPost("{id:int}/payments")]
    [Authorize(Roles = "Receptionist,Manager")]
    [ProducesResponseType(typeof(ApiResponse<PaymentReceiptDTO>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmPayment(int id, ConfirmPaymentRequestDTO request, CancellationToken cancellationToken)
    {
        var receipt = await subscriptions.ConfirmPaymentAsync(ActorId(), id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<PaymentReceiptDTO>.CreatedAt(receipt, "Payment confirmed and subscription activated."));
    }

    private int ActorId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new BusinessException(401, "A valid user identifier is required.");

    /// <summary>
    /// Xem trước thông tin và số tiền chênh lệch khi nâng cấp gói
    /// </summary>
    [HttpGet("upgrade-preview")]
    [Authorize(Roles = "Member,Receptionist,Manager")]
    [ProducesResponseType(typeof(ApiResponse<UpgradePreviewDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpgradePreview(
        [FromQuery] int targetPackageId,
        [FromQuery] int? memberId,
        CancellationToken cancellationToken)
    {
        var result = await subscriptions.GetUpgradePreviewAsync(ActorId(), memberId, targetPackageId, cancellationToken);
        return Ok(ApiResponse<UpgradePreviewDTO>.Ok(result, "Lấy thông tin xem trước nâng cấp thành công."));
    }
}
