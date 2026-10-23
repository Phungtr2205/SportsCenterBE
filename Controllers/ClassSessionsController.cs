using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Classes;
using SportsCenterAPI.DTOs.Response;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/class-sessions")]
[Authorize]
public class ClassSessionsController : BaseApiController
{
    private readonly IClassService _classService;

    public ClassSessionsController(IClassService classService)
    {
        _classService = classService;
    }

    // 1. Tìm kiếm các buổi học.
    // Ai cũng có thể xem, không cần đăng nhập.
    // GET /api/class-sessions
    /// <summary>
    /// Tìm kiếm buổi học theo bộ lọc và phân trang
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] ClassSearch search,
        CancellationToken cancellationToken)
    {
        var sessions = await _classService.SearchSessionsAsync(
            search,
            ct: cancellationToken);

        return Success(sessions, "Lấy danh sách buổi học thành công.");
    }

    // 2. HLV xem các buổi học mình phụ trách.
    // GET /api/class-sessions/mine
    /// <summary>
    /// Xem lịch dạy của huấn luyện viên đang đăng nhập
    /// </summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Coach")]
    public async Task<IActionResult> Mine(
        [FromQuery] ClassSearch search,
        CancellationToken cancellationToken)
    {
        var coachUserId = ActorId();

        var sessions = await _classService.SearchSessionsAsync(
            search,
            actorCoachId: coachUserId,
            ct: cancellationToken);

        return Success(sessions, "Lấy lịch dạy thành công.");
    }

    // 3. Hội viên tự đăng ký một buổi học.
    // POST /api/class-sessions/5/registrations
    /// <summary>
    /// Đăng ký một buổi học cho tôi
    /// </summary>
    [HttpPost("{id:int}/registrations")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> Book(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var userId = ActorId();

        var registration = await _classService.BookAsync(
            actorId: userId,
            sessionId: id,
            ct: cancellationToken);

        var response = ApiResponse<RegistrationDTO>.CreatedAt(
            registration,
            "Đặt lịch thành công.");

        return StatusCode(StatusCodes.Status201Created, response);
    }

    // 4. Lễ tân hoặc quản lý đăng ký buổi học cho hội viên.
    // POST /api/class-sessions/5/registrations/members/10
    /// <summary>
    /// Đăng ký buổi học thay cho hội viên
    /// </summary>
    [HttpPost("{id:int}/registrations/members/{memberId:int}")]
    [Authorize(Roles = "Receptionist,Manager")]
    public async Task<IActionResult> BookForMember(
        [FromRoute] int id,
        [FromRoute] int memberId,
        CancellationToken cancellationToken)
    {
        var staffUserId = ActorId();

        var registration = await _classService.BookAsync(
            actorId: staffUserId,
            sessionId: id,
            memberId: memberId,
            ct: cancellationToken);

        var response = ApiResponse<RegistrationDTO>.CreatedAt(
            registration,
            "Đã đặt lịch cho hội viên.");

        return StatusCode(StatusCodes.Status201Created, response);
    }

    // 5. Xem danh sách học viên của một buổi học.
    // Service kiểm tra HLV có phụ trách buổi học này không.
    // GET /api/class-sessions/5/roster
    /// <summary>
    /// Xem danh sách học viên của buổi học
    /// </summary>
    [HttpGet("{id:int}/roster")]
    [Authorize(Roles = "Coach,Receptionist,Manager")]
    public async Task<IActionResult> Roster(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var userId = ActorId();

        var students = await _classService.GetRosterAsync(
            userId,
            id,
            cancellationToken);

        return Success(students, "Lấy danh sách học viên thành công.");
    }

    // 6. Quản lý hủy toàn bộ buổi học.
    // Đây không phải thao tác hội viên hủy đăng ký cá nhân.
    // POST /api/class-sessions/5/cancel
    /// <summary>
    /// Quản lý hủy toàn bộ buổi học
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Cancel(
        [FromRoute] int id,
        [FromBody] CancelRequest request,
        CancellationToken cancellationToken)
    {
        var managerUserId = ActorId();

        await _classService.CancelSessionAsync(
            managerUserId,
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    // 7. Đánh dấu buổi học đã hoàn thành.
    // Service kiểm tra thời gian và quyền thao tác.
    // POST /api/class-sessions/5/complete
    /// <summary>
    /// Đánh dấu buổi học đã hoàn thành
    /// </summary>
    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = "Coach,Receptionist,Manager")]
    public async Task<IActionResult> Complete(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var userId = ActorId();

        await _classService.CompleteSessionAsync(
            userId,
            id,
            cancellationToken);

        return NoContent();
    }

    // 8. Xem đánh giá của buổi học, có phân trang.
    // GET /api/class-sessions/5/reviews?page=1&pageSize=20
    /// <summary>
    /// Xem các đánh giá của buổi học
    /// </summary>
    [HttpGet("{id:int}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> Reviews(
        [FromRoute] int id,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var reviews = await _classService.GetReviewsAsync(
            id,
            page,
            pageSize,
            cancellationToken);

        return Success(reviews, "Lấy danh sách đánh giá thành công.");
    }
}