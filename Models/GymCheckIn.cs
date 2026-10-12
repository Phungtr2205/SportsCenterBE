using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.Models;

/// <summary>
/// Thực thể ghi nhận lịch sử ra vào trung tâm của hội viên
/// </summary>
public class GymCheckIn
{
    public int Id { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int? SubscriptionId { get; set; }
    public MemberSubscription? Subscription { get; set; }

    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    public DateTime? CheckOutTime { get; set; }

    // Nhân viên lễ tân thực hiện quẹt thẻ/điểm danh
    public int? CheckedInByUserId { get; set; }
    public User? CheckedByUser { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }
}