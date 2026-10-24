using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Profile;

public record ProfileDTO(
    int UserId,
    string Email,
    string FullName,
    string? PhoneNumber,
    string RoleName,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DateOfBirth = null,
    string? Gender = null,
    string? Address = null,
    string? FitnessGoal = null,
    string? EmergencyContact = null
);

public class UpdateProfileDTO
{
    [StringLength(200, MinimumLength = 1)]
    public string? FullName { get; set; }

    [Phone]
    [StringLength(30)]
    public string? PhoneNumber { get; set; }

    [StringLength(72, MinimumLength = 8)]
    public string? Password { get; set; }

    public string? CurrentPassword { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(10)]
    public string? Gender { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? FitnessGoal { get; set; }

    [StringLength(100)]
    public string? EmergencyContact { get; set; }
}

public class RequestChangeEmailOtpDTO
{
    [Required(ErrorMessage = "Vui lòng nhập email mới.")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
    [StringLength(254)]
    public string NewEmail { get; set; } = string.Empty;
}

public class VerifyChangeEmailOtpDTO
{
    [Required(ErrorMessage = "Vui lòng nhập email mới.")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
    [StringLength(254)]
    public string NewEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã xác nhận OTP.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 chữ số.")]
    public string Otp { get; set; } = string.Empty;
}

public class AvatarUploadDTO
{
    [Required]
    public IFormFile File { get; set; } = null!;
}