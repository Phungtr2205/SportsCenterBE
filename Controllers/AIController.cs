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
