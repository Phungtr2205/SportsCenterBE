using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Auth;

public class VerifyRegisterOtpDTO
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "OTP phải gồm 6 chữ số.")]
    public string Otp { get; set; } = string.Empty;
}
