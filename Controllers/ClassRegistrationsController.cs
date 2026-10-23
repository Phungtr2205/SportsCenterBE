using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Classes;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/class-registrations")]
[Authorize]
public class ClassRegistrationsController : BaseApiController
{
    private readonly IClassService _classService;

    public ClassRegistrationsController(IClassService classService)
    {
        _classService = classService;
    }

    // 1. Hội viên xem các đăng ký lớp của mình.
    // GET /api/class-registrations/mine?page=1&pageSize=20
    /// <summary>
    /// Xem lịch sử đăng ký buổi học của tôi
    /// </summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> Mine(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = ActorId();

        // memberId = null: service tìm hội viên từ userId.
        var registrations = await _classService.GetRegistrationsAsync(
            actorId: userId,
            memberId: null,
            page: page,
            pageSize: pageSize,
            ct: cancellationToken);

        return Success(registrations);
    }

    // 2. Lễ tân hoặc quản lý xem đăng ký của một hội viên.
    // GET /api/class-registrations/members/10?page=1&pageSize=20
    /// <summary>
    /// Xem lịch sử đăng ký buổi học của một hội viên
    /// </summary>
    [HttpGet("members/{memberId:int}")]
    [Authorize(Roles = "Receptionist,Manager")]
    public async Task<IActionResult> ForMember(
        [FromRoute] int memberId,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var staffUserId = ActorId();

        var registrations = await _classService.GetRegistrationsAsync(
            actorId: staffUserId,
            memberId: memberId,
            page: page,
            pageSize: pageSize,
            ct: cancellationToken);

        return Success(registrations);
    }

    // 3. Hủy một đăng ký và giải phóng chỗ.
    // Service kiểm tra quyền sở hữu và hạn hủy.
    // POST /api/class-registrations/5/cancel
    /// <summary>
    /// Hủy đăng ký buổi học và giải phóng chỗ
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "Member,Receptionist,Manager")]
    public async Task<IActionResult> Cancel(
        [FromRoute] int id,
        [FromBody] CancelRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ActorId();

        var registration = await _classService.CancelRegistrationAsync(
            userId,
            id,
            request,
            cancellationToken);

        return Success(
            registration,
            "Đã hủy và giải phóng chỗ.");
    }

    // 4. Check-in cho một đăng ký lớp.
    // Service kiểm tra quyền và thời gian được phép check-in.
    // POST /api/class-registrations/5/check-in
    /// <summary>
    /// Điểm danh theo đăng ký buổi học
    /// </summary>
    [HttpPost("{id:int}/check-in")]
    [Authorize(Roles = "Member,Coach,Receptionist,Manager")]
    public async Task<IActionResult> CheckIn(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var userId = ActorId();

        var attendance = await _classService.CheckInAsync(
            userId,
            id,
            cancellationToken);

        return Success(attendance, "Đã điểm danh.");
    }

    // 5. Hội viên đánh giá buổi học mình đã tham gia.
    // Service kiểm tra buổi học đã hoàn thành và có điểm danh.
    // POST /api/class-registrations/5/review
    /// <summary>
    /// Đánh giá buổi học đã tham gia
    /// </summary>
    [HttpPost("{id:int}/review")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> Review(
        [FromRoute] int id,
        [FromBody] ReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ActorId();

        var review = await _classService.ReviewAsync(
            userId,
            id,
            request,
            cancellationToken);

        return Success(review);
    }
}