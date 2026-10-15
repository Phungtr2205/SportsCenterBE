using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Accounts;
using SportsCenterAPI.DTOs.Response;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/users/staff")]
[Authorize(Roles = UserRoles.Manager)]
public class UsersController : ControllerBase
{
    private readonly IAccountService _accountService;

    public UsersController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    // Lấy danh sách nhân viên.
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StaffResponseDTO>>>> GetAll()
    {
        var staff = await _accountService.GetStaffAsync();

        return Ok(
            ApiResponse<IReadOnlyList<StaffResponseDTO>>.Ok(
                staff,
                "Lấy danh sách nhân viên thành công."));
    }

    // Lấy thông tin một nhân viên.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<StaffResponseDTO>>> GetById(
        int id)
    {
        var staff = await _accountService.GetStaffAsync(id);

        return Ok(
            ApiResponse<StaffResponseDTO>.Ok(
                staff,
                "Lấy thông tin nhân viên thành công."));
    }

    // Tạo nhân viên và ghi nhận người thực hiện.
    [HttpPost]
    [ProducesResponseType(
        typeof(ApiResponse<StaffResponseDTO>),
        StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<StaffResponseDTO>>> Create(
        [FromBody] CreateStaffRequestDTO request)
    {
        var callerUserId = GetCurrentUserId();

        var staff = await _accountService.CreateStaffAsync(request, callerUserId);

        return CreatedAtAction(
            nameof(GetById),
            new { id = staff.UserId },
            ApiResponse<StaffResponseDTO>.CreatedAt(
                staff,
                "Tạo nhân viên thành công."));
    }

    // Thay đổi quyền nhân viên.
    [HttpPut("{id:int}/role")]
    public async Task<ActionResult<ApiResponse<StaffResponseDTO>>> UpdateRole(
        int id,
        [FromBody] UpdateStaffRoleRequestDTO request)
    {
        var callerUserId = GetCurrentUserId();

        var staff = await _accountService.UpdateStaffRoleAsync(
            id,
            request,
            callerUserId);

        return Ok(
            ApiResponse<StaffResponseDTO>.Ok(
                staff,
                "Cập nhật quyền nhân viên thành công."));
    }

    // Vô hiệu hóa nhân viên, giữ lại dữ liệu lịch sử.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var callerUserId = GetCurrentUserId();

        await _accountService.DeactivateStaffAsync(
            id,
            callerUserId);

        return NoContent();
    }

    // Lấy ID người đang đăng nhập từ JWT.
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