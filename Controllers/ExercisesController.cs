using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Exercises;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ExercisesController(IExerciseService exerciseService) : BaseApiController
{
    /// <summary>
    /// Lấy danh sách kho bài tập (có tìm kiếm và lọc nhóm cơ)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetExercises([FromQuery] string? muscleGroup, [FromQuery] string? search, CancellationToken ct)
    {
        var result = await exerciseService.GetExercisesAsync(muscleGroup, search, ct);
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Lấy danh sách các nhóm cơ để làm dropdown bộ lọc
    /// </summary>
    [HttpGet("muscle-groups")]
    public async Task<IActionResult> GetMuscleGroups(CancellationToken ct)
    {
        var result = await exerciseService.GetMuscleGroupsAsync(ct);
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Lấy chi tiết một bài tập
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetExerciseById(int id, CancellationToken ct)
    {
        var result = await exerciseService.GetExerciseByIdAsync(id, ct);
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Huấn luyện viên hoặc Quản lý thêm bài tập mới vào kho
    /// </summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.Coach + "," + UserRoles.Manager)]
    public async Task<IActionResult> CreateExercise([FromBody] CreateExerciseDTO request, CancellationToken ct)
    {
        var result = await exerciseService.CreateExerciseAsync(request, ct);
        return StatusCode(201, new { success = true, message = "Thêm bài tập thành công.", data = result });
    }

    /// <summary>
    /// Huấn luyện viên hoặc Quản lý sửa thông tin bài tập
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRoles.Coach + "," + UserRoles.Manager)]
    public async Task<IActionResult> UpdateExercise(int id, [FromBody] UpdateExerciseDTO request, CancellationToken ct)
    {
        var result = await exerciseService.UpdateExerciseAsync(id, request, ct);
        return Ok(new { success = true, message = "Cập nhật bài tập thành công.", data = result });
    }

    /// <summary>
    /// Huấn luyện viên hoặc Quản lý xóa bài tập (chỉ xóa được nếu chưa nằm trong giáo án nào)
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = UserRoles.Coach + "," + UserRoles.Manager)]
    public async Task<IActionResult> DeleteExercise(int id, CancellationToken ct)
    {
        await exerciseService.DeleteExerciseAsync(id, ct);
        return Ok(new { success = true, message = "Đã xóa bài tập khỏi kho thành công." });
    }
}