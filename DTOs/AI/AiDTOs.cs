using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.AI;

public class WorkoutAiRequest
{
    // Member để null; Coach nhập ID học viên mình phụ trách.
    [Range(1, int.MaxValue)]
    public int? MemberId { get; set; }

    [StringLength(200)]
    public string? Goal { get; set; }

    [Required, RegularExpression("^(Beginner|Intermediate|Advanced)$")]
    public string Level { get; set; } = "Beginner";

    [Range(1, 7)]
    public int DaysPerWeek { get; set; } = 3;
}

public class ChatAiRequest
{
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Question { get; set; } = string.Empty;
}
