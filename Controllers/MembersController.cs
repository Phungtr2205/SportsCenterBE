using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Accounts;
using SportsCenterAPI.DTOs.Register;
using SportsCenterAPI.DTOs.Response;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/members")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IAuthService _authService;

    public MembersController(
        IAccountService accountService,
        IAuthService authService)
    {
        _accountService = accountService;
        _authService = authService;
    }

    // Lễ tân hoặc quản lý đăng ký hội viên tại quầy.
    /// <summary>
    /// Lễ tân đăng ký tài khoản hội viên tại quầy
    /// </summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.FrontDesk)]
    [ProducesResponseType(
        typeof(ApiResponse<MemberResponseDTO>),
        StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<MemberResponseDTO>>> RegisterAtDesk(
        [FromBody] RegisterRequestDTO request)
    {
        var member = await _authService.RegisterAtDeskAsync(request);

        return CreatedAtAction(
            nameof(GetById),
            new { id = member.MemberId },
            ApiResponse<MemberResponseDTO>.CreatedAt(
                member,
                "Đăng ký hội viên thành công."));
    }

    // Lấy danh sách hội viên.
    /// <summary>
    /// Lễ tân xem danh sách hội viên
    /// </summary>
    [HttpGet]
    [Authorize(Roles = UserRoles.FrontDesk)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MemberResponseDTO>>>> GetAll()
    {
        var members = await _accountService.GetMembersAsync();

        return Ok(
            ApiResponse<IReadOnlyList<MemberResponseDTO>>.Ok(
                members,
                "Lấy danh sách hội viên thành công."));
    }

    // Lấy thông tin hội viên theo ID.
    /// <summary>
    /// Lễ tân xem thông tin một hội viên
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = UserRoles.FrontDesk)]
    public async Task<ActionResult<ApiResponse<MemberResponseDTO>>> GetById(
        int id)
    {
        var member = await _accountService.GetMemberAsync(id);

        return Ok(
            ApiResponse<MemberResponseDTO>.Ok(
                member,
                "Lấy thông tin hội viên thành công."));
    }

    // Hội viên xem hồ sơ của chính mình.
    /// <summary>
    /// Xem hồ sơ hội viên của tôi
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = UserRoles.Member)]
    public async Task<ActionResult<ApiResponse<MemberResponseDTO>>> GetMe()
    {
        var userId = GetCurrentUserId();

        var member = await _accountService.GetMyMemberAsync(userId);

        return Ok(
            ApiResponse<MemberResponseDTO>.Ok(
                member,
                "Lấy hồ sơ hội viên thành công."));
    }

    // Quản lý vô hiệu hóa hội viên.
    /// <summary>
    /// Vô hiệu hóa tài khoản hội viên
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = UserRoles.Manager)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var callerUserId = GetCurrentUserId();

        await _accountService.DeactivateMemberAsync(id, callerUserId);

        return NoContent();
    }

    // Lấy ID tài khoản đang đăng nhập từ JWT.
    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out var userId) || userId <= 0)
        {
            throw new BusinessException(
                401,
                "Thông tin đăng nhập không hợp lệ.");
        }

        return userId;
    }
}