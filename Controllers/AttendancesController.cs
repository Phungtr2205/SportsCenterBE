using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Attendances;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AttendancesController(
    IAttendanceService attendanceService) : BaseApiController
{
    /// <summary>
    /// Ghi nhận điểm danh học viên trong buổi học
    /// </summary>
    [HttpPost("mark")]
    [Authorize(Roles = UserRoles.FrontDesk + "," + UserRoles.Coach)]
    public async Task<IActionResult> MarkAttendance(
        [FromBody] MarkAttendanceDTO request,
        CancellationToken ct)
    {
        var result = await attendanceService.MarkAttendanceAsync(
            ActorId(),
            request,
            ct);

        return Ok(new
        {
            message = "Điểm danh thành công",
            data = result
        });
    }

    /// <summary>
    /// Xem danh sách điểm danh của một buổi học
    /// </summary>
    [HttpGet("session/{sessionId:int}")]
    [Authorize(Roles = UserRoles.FrontDesk + "," + UserRoles.Coach)]
    public async Task<IActionResult> GetSessionAttendances(
        int sessionId,
        CancellationToken ct)
    {
        var result = await attendanceService.GetSessionAttendancesAsync(
            ActorId(),
            sessionId,
            ct);

        return Ok(new
        {
            message = "Lấy danh sách điểm danh thành công",
            data = result
        });
    }

    /// <summary>
    /// Xem lịch sử điểm danh của tôi
    /// </summary>
    [HttpGet("mine")]
    [Authorize(Roles = UserRoles.Member)]
    public async Task<IActionResult> GetMyAttendances(
        CancellationToken ct)
    {
        var result = await attendanceService.GetMyAttendancesAsync(
            ActorId(),
            ct);

        return Ok(new
        {
            message = "Lấy kết quả điểm danh của hội viên thành công",
            data = result
        });
    }
}