using SportsCenterAPI.DTOs.Exercises;

namespace SportsCenterAPI.Services.Interface;

public interface IExerciseService
{
    Task<IEnumerable<ExerciseResponseDTO>> GetExercisesAsync(string? muscleGroup = null, string? search = null, CancellationToken ct = default);
    Task<ExerciseResponseDTO> GetExerciseByIdAsync(int id, CancellationToken ct = default);
    Task<ExerciseResponseDTO> CreateExerciseAsync(CreateExerciseDTO request, CancellationToken ct = default);
    Task<ExerciseResponseDTO> UpdateExerciseAsync(int id, UpdateExerciseDTO request, CancellationToken ct = default);
    Task<bool> DeleteExerciseAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<string>> GetMuscleGroupsAsync(CancellationToken ct = default);
}