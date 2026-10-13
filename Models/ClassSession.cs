using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.Models;

public class ClassSession
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public SportClass Class { get; set; } = null!;
    public int CoachId { get; set; }
    public Coach Coach { get; set; } = null!;
    public int CancellationPolicyId { get; set; }
    public CancellationPolicy CancellationPolicy { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int Capacity { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Scheduled";
    [MaxLength(500)] public string? CancellationReason { get; set; }
    public ICollection<ClassRegistration> Registrations { get; set; } = new List<ClassRegistration>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    // Gán phòng tập cho buổi học
    public int? RoomId { get; set; }
    public Room? Room { get; set; }
}
