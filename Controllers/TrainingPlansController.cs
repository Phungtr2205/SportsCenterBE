using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Training;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TrainingPlansController(ITrainingPlanService trainingService) : BaseApiController
{
    [HttpGet("exercises")]
    public async Task<IActionResult> GetExercisesCatalog()
    {
        var result = await trainingService.GetExercisesCatalogAsync();
        return Ok(new { success = true, data = result });
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.Coach)]
    public async Task<IActionResult> CreatePlan([FromBody] CreateTrainingPlanDTO request)
    {
        try
        {
            var result = await trainingService.CreatePlanAsync(ActorId(), request);
            return StatusCode(201, new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("members/{memberId}")]
    [Authorize(Roles = UserRoles.Coach + "," + UserRoles.Member + "," + UserRoles.FrontDesk)]
    public async Task<IActionResult> GetMemberPlans(int memberId)
    {
        var result = await trainingService.GetMemberPlansAsync(memberId);
        return Ok(new { success = true, data = result });
    }

    [HttpPut("exercises/{planExerciseId}/result")]
    [Authorize(Roles = UserRoles.Coach)]
    public async Task<IActionResult> UpdateResult(int planExerciseId, [FromBody] UpdateExerciseResultDTO request)
    {
        try
        {
            await trainingService.UpdateExerciseResultAsync(ActorId(), planExerciseId, request);
            return Ok(new { success = true, message = "Cập nhật tiến độ tập luyện thành công!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = UserRoles.Coach)]
    public async Task<IActionResult> UpdateStatus(int id, [FromQuery] string status = "Completed")
    {
        try
        {
            await trainingService.UpdatePlanStatusAsync(ActorId(), id, status);
            return Ok(new { success = true, message = "Đã cập nhật trạng thái giáo án." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}