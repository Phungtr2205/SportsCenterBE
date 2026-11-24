using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.Models;

public class CoachReview
{
    public int Id { get; set; }
    public int CoachId { get; set; }
    public Coach Coach { get; set; } = null!;
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int Rating { get; set; }
    [MaxLength(2000)] public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}