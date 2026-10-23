using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SportsCenterAPI.Services.Interface;
using SportsCenterAPI.Helpers;

namespace SportsCenterAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = UserRoles.Manager)] // Chỉ cho phép Manager mới được xem báo cáo
public class  ReportsController(IReportService reportService) : ControllerBase
{
    /// <summary>
    /// Xem báo cáo tổng quan hoạt động trung tâm
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var result = await reportService.GetOverviewAsync();
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Xem doanh thu từng tháng theo năm
    /// </summary>
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue([FromQuery] int? year)
    {
        int targetYear = year ?? DateTime.UtcNow.Year; // Nếu không có năm được cung cấp, sử dụng năm hiện tại
        var result = await reportService.GetRevenueOverTimeAsync(targetYear);
        return Ok(new { success = true, year = targetYear, data = result });
    }

    /// <summary>
    /// Xem thống kê tình trạng đăng ký lớp học
    /// </summary>
    [HttpGet("class-status")]
    public async Task<IActionResult> GetClassStatus()
    {
        var result = await reportService.GetClassRegistrationStatusAsync();
        return Ok(new { success = true, data = result });
    }
}

