using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.CheckIn;
using SportsCenterAPI.Services.Interface;
using System.Security.Claims;

namespace SportsCenterAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class GymCheckInsController : ControllerBase
{
    private readonly IGymCheckInService _checkInService;

    public GymCheckInsController(IGymCheckInService checkInService)
    {
        _checkInService = checkInService;
    }

    // 1. Hội viên mở app lấy mã thẻ QR của chính mình
    /// <summary>
    /// Lấy mã QR hội viên của tôi để check-in tại trung tâm
    /// </summary>
    [HttpGet("my-qr")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> GetMyQr()
    {
        var userIdStr = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var result = await _checkInService.GetMyQrCodeAsync(int.Parse(userIdStr));
        return Ok(result);
    }

    // 2. Lễ tân quét mã QR của hội viên tại cổng vào
    /// <summary>
    /// Quét mã QR để ghi nhận hội viên vào trung tâm
    /// </summary>
    [HttpPost("scan-qr")]
    [Authorize(Roles = "Receptionist,Manager")]
    public async Task<IActionResult> ScanQr([FromBody] ScanQrRequest request)
    {
        var userIdStr = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var result = await _checkInService.ScanQrCheckInAsync(int.Parse(userIdStr), request);
        return Ok(result);
    }

    // 3. Lễ tân nhập tay MemberId hoặc SĐT (phương án phụ nếu không quét QR)
    /// <summary>
    /// Ghi nhận hội viên vào trung tâm bằng mã hội viên hoặc số điện thoại
    /// </summary>
    [HttpPost("check-in")]
    [Authorize(Roles = "Receptionist,Manager")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
    {
        var userIdStr = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var result = await _checkInService.CheckInMemberAsync(int.Parse(userIdStr), request);
        return Ok(result);
    }

    // 4. Check-out khi khách ra về
    /// <summary>
    /// Ghi nhận hội viên rời trung tâm
    /// </summary>
    [HttpPut("{id}/check-out")]
    [Authorize(Roles = "Receptionist,Manager")]
    public async Task<IActionResult> CheckOut(int id)
    {
        var result = await _checkInService.CheckOutMemberAsync(id);
        return Ok(result);
    }

    // 5. Xem danh sách khách đã vào trong ngày
    /// <summary>
    /// Xem danh sách hội viên check-in trong ngày
    /// </summary>
    [HttpGet("today")]
    [Authorize(Roles = "Receptionist,Manager")]
    public async Task<IActionResult> GetTodayCheckIns()
    {
        var result = await _checkInService.GetTodayCheckInsAsync();
        return Ok(result);
    }

    // 6. Hội viên xem lịch sử điểm danh của mình
    /// <summary>
    /// Xem lịch sử ra vào trung tâm của tôi
    /// </summary>
    [HttpGet("my-history")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> GetMyHistory()
    {
        var userIdStr = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var result = await _checkInService.GetMyHistoryAsync(int.Parse(userIdStr));
        return Ok(result);
    }
}