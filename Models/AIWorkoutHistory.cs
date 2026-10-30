using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.Models;

public class AIWorkoutHistory
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    [MaxLength(200)]
    public string Goal { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Level { get; set; } = string.Empty;
    public int DaysPerWeek { get; set; }
    public string PlanJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
