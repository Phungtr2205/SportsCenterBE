using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Accounts;

public class CreateStaffRequestDTO
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(72, MinimumLength = 8, ErrorMessage = "Mật khẩu phải từ 8 đến 72 ký tự, tối đa 72 byte UTF-8.")]
    public string Password { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    [Required, RegularExpression("^(Manager|Receptionist|Coach)$",
        ErrorMessage = "Role must be Manager, Receptionist or Coach.")]
    public string Role { get; set; } = string.Empty;
}
