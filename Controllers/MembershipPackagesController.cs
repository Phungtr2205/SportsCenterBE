using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.MembershipPackages;
using SportsCenterAPI.DTOs.Response;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/membership-packages")]
[Authorize(Roles = "Manager")]
public class MembershipPackagesController(IMembershipPackageService service) : ControllerBase
{
    /// <summary>
    /// Xem danh sách gói tập đang hoạt động
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MembershipPackageDTO>>>> GetActive(CancellationToken cancellationToken)
    {
        var packages = await service.GetActiveAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<MembershipPackageDTO>>.Ok(packages, "Active membership packages retrieved."));
    }

    /// <summary>
    /// Xem chi tiết gói tập đang hoạt động
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<MembershipPackageDTO>>> GetById(int id, CancellationToken cancellationToken)
    {
        var package = await service.GetActiveByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<MembershipPackageDTO>.Ok(package, "Membership package retrieved."));
    }

    /// <summary>
    /// Quản lý xem toàn bộ danh sách gói tập
    /// </summary>
    [HttpGet("manage")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MembershipPackageDTO>>>> GetForManagement(CancellationToken cancellationToken)
    {
        var packages = await service.GetForManagementAsync(GetActorId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<MembershipPackageDTO>>.Ok(packages, "All membership packages retrieved."));
    }

    /// <summary>
    /// Quản lý xem chi tiết một gói tập
    /// </summary>
    [HttpGet("manage/{id:int}")]
    public async Task<ActionResult<ApiResponse<MembershipPackageDTO>>> GetForManagementById(int id, CancellationToken cancellationToken)
    {
        var package = await service.GetForManagementByIdAsync(id, GetActorId(), cancellationToken);
        return Ok(ApiResponse<MembershipPackageDTO>.Ok(package, "Membership package retrieved."));
    }

    /// <summary>
    /// Tạo gói tập mới
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<MembershipPackageDTO>>> Create(MembershipPackageRequestDTO request, CancellationToken cancellationToken)
    {
        var package = await service.CreateAsync(request, GetActorId(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = package.Id },
            ApiResponse<MembershipPackageDTO>.CreatedAt(package, "Membership package created."));
    }

    /// <summary>
    /// Cập nhật thông tin gói tập
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<MembershipPackageDTO>>> Update(int id, UpdateMembershipPackageRequestDTO request, CancellationToken cancellationToken)
    {
        var package = await service.UpdateAsync(id, request, GetActorId(), cancellationToken);
        return Ok(ApiResponse<MembershipPackageDTO>.Ok(package, "Membership package updated."));
    }

    /// <summary>
    /// Ngừng cung cấp gói tập
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await service.DeactivateAsync(id, GetActorId(), cancellationToken);
        return NoContent();
    }

    private int GetActorId()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId) || actorId <= 0)
            throw new BusinessException(401, "A valid user identity is required.");
        return actorId;
    }
}
