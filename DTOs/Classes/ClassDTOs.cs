using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Classes;

public class ClassRequest
{
    [Required, StringLength(200)] public string ClassName { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int SportId { get; set; }
    [Range(1, int.MaxValue)] public int CoachId { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99")] public decimal Price { get; set; }
    [Range(1, 10000)] public int MaxCapacity { get; set; }
    [StringLength(1000)] public string? Schedule { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SessionRequest
{
    [Range(1, int.MaxValue)] public int CoachId { get; set; }
    [Range(1, int.MaxValue)] public int CancellationPolicyId { get; set; } = 1;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    [Range(1, 10000)] public int Capacity { get; set; }
}

public class PolicyRequest
{
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [Range(2, 720)] public int MinimumHoursBeforeStart { get; set; } = 2;
    public bool IsActive { get; set; } = true;
}

public class CancelRequest
{
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
}

public class ReviewRequest
{
    [Range(1, 5)] public int Rating { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
}

public class ClassSearch
{
    [StringLength(200)] public string? Search { get; set; }
    [Range(1, int.MaxValue)] public int? SportId { get; set; }
    [Range(1, int.MaxValue)] public int? CoachId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public bool OnlyAvailable { get; set; }
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public record ClassDTO(int Id, string ClassName, int SportId, string SportName, int? CoachId,
    string? CoachName, decimal Price, int MaxCapacity, string? Schedule, DateTime StartDate, DateTime EndDate, bool IsActive);
public record SessionDTO(int Id, int ClassId, string ClassName, int CoachId, string CoachName,
    DateTime StartsAt, DateTime EndsAt, int Capacity, int CurrentEnrollment, int AvailableSeats,
    string Status, int CancellationPolicyId, int MinimumHoursBeforeStart);
public record RegistrationDTO(int Id, int MemberId, int ClassId, int? SessionId, string ClassName,
    DateTime? StartsAt, DateTime? EndsAt, string Status, DateTime RegistrationDate,
    int? CreatedByUserId, DateTime? CancellationDeadline, DateTime? CancelledAt, int? CancelledByUserId, string? CancellationReason,
    string? SessionStatus = null, string? AttendanceStatus = null, DateTime? CheckInTime = null,
    bool HasReviewed = false, int? Rating = null, ReviewDTO? Review = null);
public record RosterDTO(int RegistrationId, int MemberId, string FullName, string RegistrationStatus, string? AttendanceStatus, DateTime? CheckInTime);
public record AttendanceDTO(int Id, int MemberId, int? SessionId, DateTime CheckInTime, string Status);
public record ReviewDTO(int Id, int RegistrationId, int Rating, string? Comment, DateTime CreatedAt);
