using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.AI;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AIController(IAIService service) : BaseApiController
{
    /// <summary>
    /// Gợi ý kế hoạch tập luyện bằng AI theo mục tiêu và trình độ
    /// </summary>
    [HttpPost("workout-recommendations")]
    [Authorize(Roles = "Member,Coach")]
    public async Task<IActionResult> Recommend(WorkoutAiRequest request, CancellationToken ct)
    {
        var plan = await service.RecommendAsync(ActorId(), request, ct);
        return Success(plan);
    }

    /// <summary>Xem lịch sử gợi ý bài tập do tôi tạo, bản mới nhất trước</summary>
    [HttpGet("workout-recommendations/history")]
    [Authorize(Roles = "Member,Coach")]
    public async Task<IActionResult> WorkoutHistory(CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Success(await service.GetWorkoutHistoryAsync(ActorId(), page, pageSize, ct));
    }

    /// <summary>Xem lại toàn bộ nội dung một bản gợi ý bài tập đã lưu</summary>
    [HttpGet("workout-recommendations/history/{id:long}")]
    [Authorize(Roles = "Member,Coach")]
    public async Task<IActionResult> WorkoutHistoryDetail(long id, CancellationToken ct)
    {
        return Success(await service.GetWorkoutHistoryDetailAsync(ActorId(), id, ct));
    }

    /// <summary>Xóa một bản gợi ý bài tập do tôi tạo</summary>
    [HttpDelete("workout-recommendations/history/{id:long}")]
    [Authorize(Roles = "Member,Coach")]
    public async Task<IActionResult> DeleteWorkoutHistory(long id, CancellationToken ct)
    {
        await service.DeleteWorkoutHistoryAsync(ActorId(), id, ct);
        return NoContent();
    }

    /// <summary>
    /// Hỏi trợ lý AI về tập luyện và dịch vụ trung tâm
    /// </summary>
    [HttpPost("chat")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> Chat(ChatAiRequest request, CancellationToken ct)
    {
        var answer = await service.ChatAsync(ActorId(), request, ct);
        return Success(new { answer });
    }

    /// <summary>Xem lịch sử chat của tôi, lượt mới nhất trước</summary>
    [HttpGet("chat/history")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> ChatHistory(CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var history = await service.GetChatHistoryAsync(ActorId(), page, pageSize, ct);
        return Success(history);
    }

    /// <summary>Xóa toàn bộ lịch sử chat của tôi</summary>
    [HttpDelete("chat/history")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> ClearChatHistory(CancellationToken ct)
    {
        await service.ClearChatHistoryAsync(ActorId(), ct);
        return NoContent();
    }
}
