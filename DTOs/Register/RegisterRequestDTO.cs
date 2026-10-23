using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Register;

/// <summary>
/// Request DTO for new member registration / DTO yêu cầu đăng ký thành viên mới
/// </summary>
public class RegisterRequestDTO
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(72, MinimumLength = 8, ErrorMessage = "Mật khẩu phải từ 8 đến 72 ký tự, tối đa 72 byte UTF-8.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    // Member-specific fields / Thông tin riêng của hội viên
    public DateTime? DateOfBirth { get; set; }

    [StringLength(10)]
    public string? Gender { get; set; }

    [StringLength(200)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? FitnessGoal { get; set; }
}
