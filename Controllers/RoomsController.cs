using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterAPI.DTOs.Rooms;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    // Tất cả mọi người (kể cả Hội viên, HLV, Lễ tân) đều có thể xem danh sách phòng
    /// <summary>
    /// Xem danh sách phòng tập theo trạng thái hoạt động
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive)
    {
        var result = await _roomService.GetAllRoomsAsync(isActive);
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết phòng tập
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _roomService.GetRoomByIdAsync(id);
        return Ok(result);
    }

    // Kiểm tra xem phòng có bị trùng lịch học vào khung giờ này không
    /// <summary>
    /// Kiểm tra phòng tập còn trống trong khoảng thời gian
    /// </summary>
    [HttpGet("{id}/check-availability")]
    public async Task<IActionResult> CheckAvailability(int id, [FromQuery] DateTime startsAt, [FromQuery] DateTime endsAt)
    {
        var isFree = await _roomService.IsRoomAvailableAsync(id, startsAt, endsAt);
        return Ok(new { roomId = id, isAvailable = isFree, message = isFree ? "Phòng còn trống vào khung giờ này." : "Phòng đã có lịch học trùng!" });
    }

    // Chỉ Quản lý trung tâm (Manager) mới có quyền tạo phòng
    /// <summary>
    /// Tạo phòng tập mới
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest request)
    {
        var result = await _roomService.CreateRoomAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // Chỉ Quản lý mới có quyền cập nhật thông tin phòng
    /// <summary>
    /// Cập nhật thông tin phòng tập
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRoomRequest request)
    {
        var result = await _roomService.UpdateRoomAsync(id, request);
        return Ok(result);
    }

    // Chỉ Quản lý mới có quyền xóa/ngưng hoạt động phòng
    /// <summary>
    /// Ngừng sử dụng phòng tập
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        await _roomService.DeleteRoomAsync(id);
        return NoContent();
    }
}