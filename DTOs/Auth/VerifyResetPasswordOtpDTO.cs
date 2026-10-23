namespace SportsCenterAPI.DTOs.Auth;

using System.ComponentModel.DataAnnotations;

public class VerifyResetPasswordOtpDTO
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "OTP phải gồm 6 chữ số.")]
    public string Otp { get; set; } = string.Empty;

    [Required, StringLength(72, MinimumLength = 8, ErrorMessage = "Mật khẩu phải từ 8 đến 72 ký tự, tối đa 72 byte UTF-8.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
