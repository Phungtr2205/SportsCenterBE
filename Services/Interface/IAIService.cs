using SportsCenterAPI.DTOs.AI;
using SportsCenterAPI.Helpers;

namespace SportsCenterAPI.Services.Interface;

public interface IAIService
{
    Task<WorkoutPlanResponse> RecommendAsync(int actorId, WorkoutAiRequest request, CancellationToken ct);
    Task<PagedResult<WorkoutHistoryItem>> GetWorkoutHistoryAsync(int actorId, int page, int pageSize, CancellationToken ct);
    Task<WorkoutHistoryDetail> GetWorkoutHistoryDetailAsync(int actorId, long id, CancellationToken ct);
    Task DeleteWorkoutHistoryAsync(int actorId, long id, CancellationToken ct);
    Task<string> ChatAsync(int actorId, ChatAiRequest request, CancellationToken ct);
    Task<PagedResult<ChatHistoryItem>> GetChatHistoryAsync(int actorId, int page, int pageSize, CancellationToken ct);
    Task ClearChatHistoryAsync(int actorId, CancellationToken ct);
}
