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

public class ChatAiRequest
{
    [Required(ErrorMessage = "Vui lòng nhập câu hỏi.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Câu hỏi tối đa 1000 ký tự.")]
    public string Question { get; set; } = string.Empty;
}
