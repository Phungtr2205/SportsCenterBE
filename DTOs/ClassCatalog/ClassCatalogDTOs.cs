using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.ClassCatalog;

public class SportRequestDTO
{
    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class CoachProfileRequestDTO
{
    [StringLength(200)]
    public string? Specialization { get; set; }

    [StringLength(2000)]
    public string? Bio { get; set; }

    [Range(0, 80)]
    public int YearsOfExperience { get; set; }
}

public class SportListDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
}

public class SportDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
}

public class CoachDTO
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string? Bio { get; set; }
    public int YearsOfExperience { get; set; }
    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

public class CoachProfileDTO
{
    public int Id { get; set; }
    public string? Specialization { get; set; }
    public string? Bio { get; set; }
    public int YearsOfExperience { get; set; }
}

public record CoachReviewDTO(
    int Id,
    int CoachId,
    int MemberId,
    string MemberName,
    int Rating,
    string? Comment,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
public record CoachReviewSummaryDTO(
    int CoachId,
    string CoachName,
    double? AverageRating,
    int ReviewCount,
    Dictionary<int, int> RatingCounts,
    IReadOnlyList<CoachReviewDTO> Items,
    int Total,
    int Page,
    int PageSize);
public class CoachReviewRequest
{
    [Range(1, 5, ErrorMessage = "Số sao đánh giá phải từ 1 đến 5.")]
    public int Rating { get; set; }

    [StringLength(2000, ErrorMessage = "Nhận xét tối đa 2000 ký tự.")]
    public string? Comment { get; set; }
}