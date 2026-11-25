using SportsCenterAPI.DTOs.Training;

namespace SportsCenterAPI.Services.Interface;

public interface ITrainingPlanService
{
    Task<DuplicatePlanCheckResponseDTO> CheckDuplicateCompletedPlanAsync(int coachUserId, CreateTrainingPlanDTO request);
    Task<TrainingPlanResponseDTO> CreatePlanAsync(int coachUserId, CreateTrainingPlanDTO request);
    Task<TrainingPlanResponseDTO> UpdatePlanAsync(int coachUserId, int planId, CreateTrainingPlanDTO request);
    Task DeletePlanAsync(int coachUserId, int planId);
    Task<IEnumerable<TrainingPlanResponseDTO>> GetMemberPlansAsync(int actorUserId, int memberId);
    Task<bool> UpdateExerciseResultAsync(int coachUserId, int planExerciseId, UpdateExerciseResultDTO request);
    Task<bool> UpdatePlanStatusAsync(int coachUserId, int planId, string status);
    Task<bool> RequestPlanChangeAsync(int memberUserId, int planId, RequestPlanChangeDTO request);
    Task<TrainingPlanResponseDTO> CompletePlanByMemberAsync(int memberUserId, int planId);
    Task<IEnumerable<object>> GetExercisesCatalogAsync();
}