using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Classes;
using SportsCenterAPI.DTOs.Response;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/cancellation-policies")]
[Authorize(Roles = "Manager")]
public class CancellationPoliciesController(IClassService service)
    : ControllerBase
{
    /// <summary>
    /// Xem danh sách chính sách hủy đăng ký buổi học
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<List<CancellationPolicy>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<CancellationPolicy>>>> GetPolicies(
        CancellationToken ct)
    {
        var policies = await service.GetPoliciesAsync(GetActorId(), ct);

        return Ok(ApiResponse<List<CancellationPolicy>>.Ok(
            policies,

            "Lấy danh sách chính sách hủy thành công."));

    }

    /// <summary>
    /// Tạo chính sách hủy đăng ký buổi học
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreatePolicy(
        PolicyRequest request,
        CancellationToken ct)
    {
        var policy = await service.SavePolicyAsync(
            GetActorId(), null, request, ct);

        return Ok(ApiResponse<CancellationPolicy>.Ok(
            policy,


            "Tạo chính sách hủy thành công."));
    }

    /// <summary>
    /// Cập nhật chính sách hủy đăng ký buổi học
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePolicy(int id,PolicyRequest request,CancellationToken ct)
    {
        var policy = await service.SavePolicyAsync(
            GetActorId(), id, request, ct);

        return Ok(ApiResponse<CancellationPolicy>.Ok(
            policy,


            "Cập nhật chính sách hủy thành công."));

    }

    private int GetActorId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out var actorId))
        {


            throw new BusinessException(401, "Vui lòng đăng nhập.");
        }

        return actorId;
    }
}