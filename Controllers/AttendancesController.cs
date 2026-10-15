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
    /// Ghi kết quả điểm danh.
    /// Chỉ Coach được gọi; service sẽ kiểm tra HLV phụ trách.
    /// ID người thao tác lấy từ JWT.
    /// </summary>
    [HttpPost("mark")]
    [Authorize(Roles = UserRoles.Coach)]
    public async Task<IActionResult> MarkAttendance(
        [FromBody] MarkAttendanceDTO request)
    {
        var result = await attendanceService.MarkAttendanceAsync(
            ActorId(),
            request);

        return Ok(new
        {
            message = "Điểm danh thành công",
            data = result
        });
    }

    /// <summary>
    /// Xem kết quả điểm danh của một buổi học.
    /// Dành cho HLV phụ trách, lễ tân và Manager.
    /// Service sẽ kiểm tra quyền trên buổi học.
    /// </summary>
    [HttpGet("session/{sessionId:int}")]
    [Authorize(Roles = UserRoles.FrontDesk + "," + UserRoles.Coach)]
    public async Task<IActionResult> GetSessionAttendances(
        int sessionId)
    {
        var result = await attendanceService.GetSessionAttendancesAsync(
            ActorId(),
            sessionId);

        return Ok(new
        {
            message = "Lấy danh sách điểm danh thành công",
            data = result
        });
    }
}