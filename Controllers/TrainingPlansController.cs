using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Training;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Services.Implement;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TrainingPlansController(ITrainingPlanService trainingService) : BaseApiController
{
    /// <summary>
    /// Xem danh mục bài tập để lập kế hoạch tập luyện
    /// </summary>
    [HttpGet("exercises")]
    public async Task<IActionResult> GetExercisesCatalog()
    {
        var result = await trainingService.GetExercisesCatalogAsync();
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Kiểm tra giáo án mới có trùng với giáo án hội viên đã hoàn thành không
    /// </summary>
    [HttpPost("check-duplicate")]
    [Authorize(Roles = UserRoles.Coach)]
    public async Task<IActionResult> CheckDuplicate([FromBody] CreateTrainingPlanDTO request)
    {
        try
        {
            var result = await trainingService.CheckDuplicateCompletedPlanAsync(ActorId(), request);
            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Huấn luyện viên tạo kế hoạch tập luyện
    /// </summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.Coach)]
    public async Task<IActionResult> CreatePlan([FromBody] CreateTrainingPlanDTO request)
    {
        try
        {
            var result = await trainingService.CreatePlanAsync(ActorId(), request);
            return StatusCode(201, new { success = true, data = result });
        }
        catch (DuplicateCompletedPlanException ex)
        {
            return Conflict(new
            {
                success = false,
                code = "DUPLICATE_COMPLETED_PLAN",
                message = ex.Message,
                matchedPlans = ex.MatchedPlans
            });
        }
        catch (BusinessException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Xem kế hoạch tập luyện của một hội viên
    /// </summary>
    [HttpGet("members/{memberId}")]
    [Authorize(Roles = UserRoles.Coach + "," + UserRoles.Member + "," + UserRoles.FrontDesk)]
    public async Task<IActionResult> GetMemberPlans(int memberId)
    {
        var result = await trainingService.GetMemberPlansAsync(ActorId(), memberId);
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Huấn luyện viên cập nhật kết quả và nhận xét bài tập
    /// </summary>
    [HttpPut("exercises/{planExerciseId}/result")]
    [Authorize(Roles = UserRoles.Coach)]
    public async Task<IActionResult> UpdateResult(int planExerciseId, [FromBody] UpdateExerciseResultDTO request)
    {
        try
        {
            await trainingService.UpdateExerciseResultAsync(ActorId(), planExerciseId, request);
            return Ok(new { success = true, message = "Cập nhật tiến độ tập luyện thành công!" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Huấn luyện viên cập nhật trạng thái kế hoạch tập luyện
    /// </summary>
    [HttpPut("{id:int}/status")]
    [Authorize(Roles = UserRoles.Coach)]
    public async Task<IActionResult> UpdateStatus(int id, [FromQuery] string status = "Completed")
    {
        try
        {
            await trainingService.UpdatePlanStatusAsync(ActorId(), id, status);
            return Ok(new { success = true, message = "Đã cập nhật trạng thái giáo án." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Huấn luyện viên sửa giáo án (tên, mục tiêu, ngày, danh sách bài tập)
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRoles.Coach)]
    public async Task<IActionResult> UpdatePlan(int id, [FromBody] CreateTrainingPlanDTO request)
    {
        try
        {
            var result = await trainingService.UpdatePlanAsync(ActorId(), id, request);
            return Ok(new { success = true, data = result });
        }
        catch (DuplicateCompletedPlanException ex)
        {
            return Conflict(new
            {
                success = false,
                code = "DUPLICATE_COMPLETED_PLAN",
                message = ex.Message,
                matchedPlans = ex.MatchedPlans
            });
        }
        catch (BusinessException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Huấn luyện viên xóa giáo án
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = UserRoles.Coach)]
    public async Task<IActionResult> DeletePlan(int id)
    {
        try
        {
            await trainingService.DeletePlanAsync(ActorId(), id);
            return Ok(new { success = true, message = "Đã xóa giáo án." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Hội viên tự xác nhận đã tập xong giáo án
    /// </summary>
    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = UserRoles.Member)]
    public async Task<IActionResult> Complete(int id)
    {
        var result = await trainingService.CompletePlanByMemberAsync(ActorId(), id);
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Hội viên gửi yêu cầu thay đổi giáo án tới huấn luyện viên
    /// </summary>
    [HttpPost("{id:int}/request-change")]
    [Authorize(Roles = UserRoles.Member)]
    public async Task<IActionResult> RequestChange(int id, [FromBody] RequestPlanChangeDTO request)
    {
        try
        {
            await trainingService.RequestPlanChangeAsync(ActorId(), id, request);
            return Ok(new { success = true, message = "Đã gửi yêu cầu thay đổi giáo án tới huấn luyện viên." });
        }
        catch (BusinessException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}