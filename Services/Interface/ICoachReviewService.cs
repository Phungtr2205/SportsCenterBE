using SportsCenterAPI.DTOs.ClassCatalog;

namespace SportsCenterAPI.Services.Interface;

public interface ICoachReviewService
{
    Task<CoachReviewDTO> CreateAsync(int actorUserId, int coachId, CoachReviewRequest request, CancellationToken ct = default);
    Task<CoachReviewDTO> UpdateMineAsync(int actorUserId, int coachId, CoachReviewRequest request, CancellationToken ct = default);
    Task<CoachReviewSummaryDTO> GetByCoachAsync(int coachId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<CoachReviewSummaryDTO> GetMineAsync(int actorUserId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}