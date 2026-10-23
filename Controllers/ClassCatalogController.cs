using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.ClassCatalog;
using SportsCenterAPI.DTOs.Response;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = "Manager")]
public class ClassCatalogController : ControllerBase
{
    private readonly IClassCatalogService _classCatalogService;

    public ClassCatalogController(
        IClassCatalogService classCatalogService)
    {
        _classCatalogService = classCatalogService;
    }

    /// <summary>
    /// Xem danh sách bộ môn thể thao
    /// </summary>
    [HttpGet("sports")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<SportListDTO>>>> GetSports(
        CancellationToken cancellationToken)
    {
        var sports = await _classCatalogService.GetSportsAsync(
            cancellationToken);

        var response = ApiResponse<List<SportListDTO>>.Ok(
            sports,
            "Thành công.");

        return Ok(response);
    }

    /// <summary>
    /// Thêm bộ môn thể thao
    /// </summary>
    [HttpPost("sports")]
    public async Task<ActionResult<ApiResponse<SportDTO>>> CreateSport(
        [FromBody] SportRequestDTO sportRequestDTO,
        CancellationToken cancellationToken)
    {
        var sport = await _classCatalogService.CreateSportAsync(
            sportRequestDTO,
            cancellationToken);

        var response = ApiResponse<SportDTO>.Ok(
            sport,
            "Thành công.");

        return Ok(response);
    }

    /// <summary>
    /// Cập nhật bộ môn thể thao
    /// </summary>
    [HttpPut("sports/{id:int}")]
    public async Task<ActionResult<ApiResponse<SportDTO>>> UpdateSport(
        int id,
        [FromBody] SportRequestDTO sportRequestDTO,
        CancellationToken cancellationToken)
    {
        var sport = await _classCatalogService.UpdateSportAsync(
            id,
            sportRequestDTO,
            cancellationToken);

        var response = ApiResponse<SportDTO>.Ok(
            sport,
            "Thành công.");

        return Ok(response);
    }

    /// <summary>
    /// Xem danh sách huấn luyện viên
    /// </summary>
    [HttpGet("coaches")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<CoachDTO>>>> GetCoaches(
        CancellationToken cancellationToken)
    {
        var coaches = await _classCatalogService.GetCoachesAsync(
            cancellationToken);

        var response = ApiResponse<List<CoachDTO>>.Ok(
            coaches,
            "Thành công.");

        return Ok(response);
    }

    /// <summary>
    /// Cập nhật hồ sơ chuyên môn của huấn luyện viên
    /// </summary>
    [HttpPut("coaches/{id:int}/profile")]
    public async Task<ActionResult<ApiResponse<CoachProfileDTO>>> UpdateCoach(
        int id,
        [FromBody] CoachProfileRequestDTO coachProfileRequestDTO,
        CancellationToken cancellationToken)
    {
        var coach = await _classCatalogService.UpdateCoachAsync(
            id,
            coachProfileRequestDTO,
            cancellationToken);

        var response = ApiResponse<CoachProfileDTO>.Ok(
            coach,
            "Thành công.");

        return Ok(response);
    }
}