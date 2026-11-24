using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Training;

public class CreateTrainingPlanDTO
{
    [Required] public string PlanName { get; set; } = string.Empty;
    [Required] public string Goal { get; set; } = string.Empty;
    [Required] public int MemberId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool ConfirmRepeat { get; set; } = false;
    public List<CreatePlanExerciseDTO> Exercises { get; set; } = new();
}

public class RequestPlanChangeDTO
{
    [Required(ErrorMessage = "Vui lòng nhập nội dung bạn muốn thay đổi.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Nội dung yêu cầu tối đa 1000 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public class CreatePlanExerciseDTO
{
    [Required] public int ExerciseId { get; set; }
    [Required] public int Sets { get; set; }
    [Required] public int Reps { get; set; }
    public int? DurationInMinutes { get; set; }
    public string? Notes { get; set; }
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
    public bool HasCompletedBefore { get; set; }
    public int? PreviousCompletedPlanId { get; set; }
    public string? RepeatNotice { get; set; }
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
    [Required] public string Notes { get; set; } = string.Empty;
}

public record MatchedCompletedPlanDTO(
    int Id,
    string PlanName,
    string Goal,
    DateTime StartDate,
    DateTime EndDate);

public record DuplicatePlanCheckResponseDTO(
    bool IsDuplicate,
    string? Message,
    IReadOnlyList<MatchedCompletedPlanDTO> MatchedPlans);