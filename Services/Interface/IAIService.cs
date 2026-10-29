using SportsCenterAPI.DTOs.AI;

namespace SportsCenterAPI.Services.Interface;

public interface IAIService
{
    Task<WorkoutPlanResponse> RecommendAsync(int actorId, WorkoutAiRequest request, CancellationToken ct);
    Task<string> ChatAsync(int actorId, ChatAiRequest request, CancellationToken ct);
}
