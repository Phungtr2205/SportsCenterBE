using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Auth;
using SportsCenterAPI.DTOs.Login;
using SportsCenterAPI.DTOs.Register;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Đăng nhập và nhận access token, refresh token
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDTO request)
    {
        var response = await authService.LoginAsync(request);
        Response.Headers.CacheControl = "no-store";
        return Ok(response);
    }
    /// <summary>
    /// Đăng nhập bằng Google (idToken hoặc code + redirectUri)
    /// </summary>
    [AllowAnonymous]
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(
        [FromBody] GoogleLoginDTO request)
    {
        var response = await authService.GoogleLoginAsync(request);
        Response.Headers.CacheControl = "no-store";
        return Ok(response);
    }

    /// <summary>
    /// Gửi OTP xác nhận đăng ký tài khoản
    /// </summary>
    [AllowAnonymous]
    [HttpPost("send-register-otp")]
    public async Task<IActionResult> SendRegisterOtp(
        [FromBody] RegisterRequestDTO request)
    {
        await authService.SendRegisterOtpAsync(request);
        return Ok(new { message = "Registration OTP sent." });
    }

    /// <summary>
    /// Xác thực OTP đăng ký và tạo tài khoản hội viên
    /// </summary>
    [AllowAnonymous]
    [HttpPost("verify-register-otp")]
    public async Task<IActionResult> VerifyRegisterOtp(
        [FromBody] VerifyRegisterOtpDTO request)
    {
        var response = await authService.VerifyRegisterOtpAsync(request);
        Response.Headers.CacheControl = "no-store";
        return Ok(response);
    }

    /// <summary>
    /// Gửi OTP đặt lại mật khẩu
    /// </summary>
    [AllowAnonymous]
    [HttpPost("request-reset-password")]
    public async Task<IActionResult> RequestResetPasswordOtp(
        [FromBody] RequestOtpDTO request)
    {
        await authService.RequestResetPasswordOtpAsync(request);

        return Ok(new
        {
            message = "If the account exists, an OTP has been sent."
        });
    }

    /// <summary>
    /// Xác thực OTP và đặt mật khẩu mới
    /// </summary>
    [AllowAnonymous]
    [HttpPost("verify-reset-password")]
    public async Task<IActionResult> VerifyResetPasswordOtp(
        [FromBody] VerifyResetPasswordOtpDTO request)
    {
        await authService.VerifyResetPasswordOtpAsync(request);
        return Ok(new { message = "Password updated successfully." });
    }
    /// <summary>
    /// Cấp access token mới và thay refresh token
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenDTO tokenDTO)
    {
        var response = await authService.RenewToken(tokenDTO);
        Response.Headers.CacheControl = "no-store";
        return Ok(response);
    }

    /// <summary>
    /// Đăng xuất và thu hồi refresh token
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenDTO tokenDTO)
    {
        await authService.Logout(tokenDTO);
        return Ok(new { message = "Signed out successfully." });
    }
}
