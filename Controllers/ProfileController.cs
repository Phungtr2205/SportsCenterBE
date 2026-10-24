using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Profile;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : BaseApiController
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    /// <summary>
    /// Xem thông tin tài khoản đang đăng nhập
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = ActorId();

        var result = await _profileService.GetAsync(userId);

        Response.Headers.CacheControl = "no-store";

        return Success(
            result,
            "Lấy thông tin cá nhân thành công."
        );
    }

    /// <summary>
    /// Cập nhật thông tin tài khoản hoặc đổi mật khẩu
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileDTO dto)
    {
        var userId = ActorId();

        var result = await _profileService.UpdateAsync(userId, dto);

        return Success(
            result,
            "Cập nhật thông tin cá nhân thành công."
        );
    }
    /// <summary>
    /// Gửi mã OTP xác nhận tới hòm thư của email mới
    /// </summary>
    [HttpPost("change-email/request-otp")]
    public async Task<IActionResult> RequestChangeEmailOtp([FromBody] RequestChangeEmailOtpDTO dto)
    {
        var userId = ActorId();
        await _profileService.SendChangeEmailOtpAsync(userId, dto);
        return Success("Mã xác nhận đã được gửi tới hộp thư của email mới. Mã có hiệu lực trong 5 phút.");
    }

    /// <summary>
    /// Xác thực mã OTP và hoàn tất đổi email tài khoản
    /// </summary>
    [HttpPost("change-email/verify-otp")]
    public async Task<IActionResult> VerifyChangeEmailOtp([FromBody] VerifyChangeEmailOtpDTO dto)
    {
        var userId = ActorId();
        var result = await _profileService.VerifyChangeEmailOtpAsync(userId, dto);
        return Success(result, "Thay đổi email tài khoản thành công! Vui lòng dùng email mới để đăng nhập.");
    }
}