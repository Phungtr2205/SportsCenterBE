using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Auth;

public class RequestOtpDTO
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}
