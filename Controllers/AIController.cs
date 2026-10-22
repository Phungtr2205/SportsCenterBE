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
    [HttpPost("workout-recommendations")]
    [Authorize(Roles = "Member,Coach")]
    public async Task<IActionResult> Recommend(WorkoutAiRequest request, CancellationToken ct)
    {
        var answer = await service.RecommendAsync(ActorId(), request, ct);
        return Success(new { answer });
    }

    [HttpPost("chat")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> Chat(ChatAiRequest request, CancellationToken ct)
    {
        var answer = await service.ChatAsync(ActorId(), request, ct);
        return Success(new { answer });
    }
}
