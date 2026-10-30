using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.Models;

/// <summary>Một lượt hỏi và trả lời AI được lưu cùng nhau cho từng tài khoản.</summary>
public class AIChatHistory
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [MaxLength(1000)]
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
