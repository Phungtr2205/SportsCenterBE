using SportsCenterAPI.DTOs.Training;

namespace SportsCenterAPI.Services.Interface;

public interface ITrainingPlanService
{
    Task<TrainingPlanResponseDTO> CreatePlanAsync(int coachUserId, CreateTrainingPlanDTO request);
    Task<IEnumerable<TrainingPlanResponseDTO>> GetMemberPlansAsync(int memberId);
    Task<bool> UpdateExerciseResultAsync(int coachUserId, int planExerciseId, UpdateExerciseResultDTO request);
    Task<bool> UpdatePlanStatusAsync(int coachUserId, int planId, string status);
    Task<TrainingPlanResponseDTO> UpdatePlanAsync(int coachUserId, int planId, CreateTrainingPlanDTO request);
    Task DeletePlanAsync(int coachUserId, int planId);
    Task<IEnumerable<object>> GetExercisesCatalogAsync();
}