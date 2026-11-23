using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/coaches")]
public class CoachesController : BaseApiController
{
    private readonly IClassService _classService;

    public CoachesController(IClassService classService) => _classService = classService;

    // Xem điểm trung bình và đánh giá của huấn luyện viên, ai cũng xem được.
    // GET /api/coaches/5/reviews?page=1&pageSize=20
    /// <summary>
    /// Xem điểm trung bình và các đánh giá của huấn luyện viên
    /// </summary>
    [HttpGet("{id:int}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> Reviews(
        [FromRoute] int id,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Success(await _classService.GetCoachReviewsAsync(id, page, pageSize, cancellationToken),
            "Lấy đánh giá huấn luyện viên thành công.");
}
