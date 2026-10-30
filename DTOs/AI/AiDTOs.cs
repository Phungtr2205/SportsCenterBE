using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.AI;

public class WorkoutAiRequest
{
    // Member để null; Coach nhập ID học viên mình phụ trách.
    [Range(1, int.MaxValue, ErrorMessage = "MemberId phải lớn hơn 0.")]
    public int? MemberId { get; set; }

    [StringLength(200, ErrorMessage = "Mục tiêu tập luyện tối đa 200 ký tự.")]
    public string? Goal { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn trình độ tập luyện.")]
    [RegularExpression("^(Beginner|Intermediate|Advanced)$", ErrorMessage = "Trình độ phải là Beginner, Intermediate hoặc Advanced.")]
    public string Level { get; set; } = "Beginner";

    [Range(1, 7, ErrorMessage = "Số ngày tập mỗi tuần phải từ 1 đến 7.")]
    public int DaysPerWeek { get; set; } = 3;
}

public class WorkoutPlanResponse
{
    public string Goal { get; set; } = "";
    public string Level { get; set; } = "";
    public int DaysPerWeek { get; set; }
    public List<WorkoutSession> Sessions { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public class WorkoutSession
{
    public int Day { get; set; }
    public string Title { get; set; } = "";
    public int? DurationMinutes { get; set; }
    public List<WorkoutExercise> Exercises { get; set; } = [];
}

public class WorkoutExercise
{
    public string ExerciseName { get; set; } = "";
    public int? Sets { get; set; }
    public int? Reps { get; set; }
    public int? RestSeconds { get; set; }
    public string? Note { get; set; }
}

public class ChatAiRequest
{
    [Required(ErrorMessage = "Vui lòng nhập câu hỏi.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Câu hỏi tối đa 1000 ký tự.")]
    public string Question { get; set; } = string.Empty;
}

public class ChatHistoryItem
{
    public long Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
