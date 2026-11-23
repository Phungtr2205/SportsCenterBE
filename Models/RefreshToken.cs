using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SportsCenterAPI.Models;

[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(PreviousTokenHash))]
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }

    // Only the SHA-256 hash is persisted; the client receives the random secret.
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    // Hash của token ngay trước lần xoay gần nhất; chỉ còn dùng lại được trong 30 giây sau khi đổi
    // để client retry khi phản hồi bị mất hoặc timeout sau khi server đã xoay token.
    [MaxLength(64)]
    public string? PreviousTokenHash { get; set; }
    public DateTime? RotatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }

    public User User { get; set; } = null!;
}
