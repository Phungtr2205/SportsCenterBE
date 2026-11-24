using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.ClassCatalog;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/coaches")]
[Authorize]
public class CoachReviewsController(ICoachReviewService service) : BaseApiController
{
    [HttpGet("{coachId:int}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByCoach(
        [FromRoute] int coachId,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await service.GetByCoachAsync(coachId, page, pageSize, ct);
        return Success(result, "Lấy danh sách đánh giá huấn luyện viên thành công.");
    }

    [HttpGet("mine/reviews")]
    [Authorize(Roles = UserRoles.Coach)]
    public async Task<IActionResult> GetMine(
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await service.GetMineAsync(ActorId(), page, pageSize, ct);
        return Success(result, "Lấy đánh giá của tôi thành công.");
    }

    [HttpPost("{coachId:int}/reviews")]
    [Authorize(Roles = UserRoles.Member)]
    public async Task<IActionResult> Create(
        [FromRoute] int coachId,
        [FromBody] CoachReviewRequest request,
        CancellationToken ct)
    {
        var created = await service.CreateAsync(ActorId(), coachId, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            DTOs.Response.ApiResponse<CoachReviewDTO>.CreatedAt(created, "Đã gửi đánh giá huấn luyện viên."));
    }

    [HttpPut("{coachId:int}/reviews")]
    [Authorize(Roles = UserRoles.Member)]
    public async Task<IActionResult> UpdateMine(
        [FromRoute] int coachId,
        [FromBody] CoachReviewRequest request,
        CancellationToken ct)
    {
        var updated = await service.UpdateMineAsync(ActorId(), coachId, request, ct);
        return Success(updated, "Đã cập nhật đánh giá huấn luyện viên.");
    }
}