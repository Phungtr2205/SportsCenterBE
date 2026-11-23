using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Training;

public class CreateTrainingPlanDTO : IValidatableObject
{
    [Required, StringLength(200)] public string PlanName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Goal { get; set; } = string.Empty; // WeightLoss, MuscleGain...
    [Range(1, int.MaxValue)] public int MemberId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    [MaxLength(50, ErrorMessage = "Một giáo án tối đa 50 bài tập.")]
    public List<CreatePlanExerciseDTO> Exercises { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (EndDate.Date < StartDate.Date)
            yield return new ValidationResult("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.", [nameof(EndDate)]);
    }
}

public class CreatePlanExerciseDTO
{
    [Range(1, int.MaxValue)] public int ExerciseId { get; set; }
    [Range(1, 100)] public int Sets { get; set; }
    [Range(1, 1000)] public int Reps { get; set; }
    [Range(1, 600)] public int? DurationInMinutes { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public class TrainingPlanResponseDTO
{
    public int Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string CoachName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<PlanExerciseResponseDTO> Exercises { get; set; } = new();
}

public class PlanExerciseResponseDTO
{
    public int Id { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public int Sets { get; set; }
    public int Reps { get; set; }
    public int? DurationInMinutes { get; set; }
    public string? Notes { get; set; }
}

public class UpdateExerciseResultDTO
{
    [Required, StringLength(1000)] public string Notes { get; set; } = string.Empty;
}