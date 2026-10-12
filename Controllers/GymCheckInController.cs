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

    // Lễ tân quẹt thẻ / nhập SĐT điểm danh khách vào cửa
    [HttpPost("check-in")]
    [Authorize(Roles = "Receptionist,Manager")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
    {
        var userIdStr = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var result = await _checkInService.CheckInMemberAsync(int.Parse(userIdStr), request);
        return Ok(result);
    }

    // Lễ tân bấm Check-out khi khách ra về
    [HttpPut("{id}/check-out")]
    [Authorize(Roles = "Receptionist,Manager")]
    public async Task<IActionResult> CheckOut(int id)
    {
        var result = await _checkInService.CheckOutMemberAsync(id);
        return Ok(result);
    }

    // Lễ tân xem danh sách khách đã vào trung tâm trong ngày hôm nay
    [HttpGet("today")]
    [Authorize(Roles = "Receptionist,Manager")]
    public async Task<IActionResult> GetTodayCheckIns()
    {
        var result = await _checkInService.GetTodayCheckInsAsync();
        return Ok(result);
    }

    // Hội viên tự xem lịch sử ra vào trung tâm của chính mình
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