using SportsCenterAPI.DTOs.Training;

namespace SportsCenterAPI.Services.Interface;

public interface ITrainingPlanService
{
    Task<TrainingPlanResponseDTO> CreatePlanAsync(int coachUserId, CreateTrainingPlanDTO request);
    Task<IEnumerable<TrainingPlanResponseDTO>> GetMemberPlansAsync(int memberId);
    Task<bool> UpdateExerciseResultAsync(int coachUserId, int planExerciseId, UpdateExerciseResultDTO request);
    Task<bool> UpdatePlanStatusAsync(int coachUserId, int planId, string status);
    Task<IEnumerable<object>> GetExercisesCatalogAsync();
}