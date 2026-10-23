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
}