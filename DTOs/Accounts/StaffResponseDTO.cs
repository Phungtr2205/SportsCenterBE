using SportsCenterAPI.Models;

namespace SportsCenterAPI.DTOs.Accounts;

public class StaffResponseDTO
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? CoachId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    internal static StaffResponseDTO FromModel(SportsCenterAPI.Models.User user) => new()
    {
        UserId = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        Phone = user.PhoneNumber,
        Role = user.Role,
        IsActive = user.IsActive,
        CoachId = user.Coach?.Id,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };
}
