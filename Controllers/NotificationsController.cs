using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.Helpers;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : BaseApiController
{
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context)
    {
        _context = context;
    }

    // 1. Xem thông báo của tài khoản đang đăng nhập.
    // GET /api/notifications?page=1&pageSize=20
    // Thêm unreadOnly=true để chỉ lấy thông báo chưa đọc.
    /// <summary>
    /// Xem thông báo của tôi, có lọc chưa đọc và phân trang
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Kiểm tra thông tin phân trang.
        if (page < 1 || page > 100000 ||
            pageSize < 1 || pageSize > 100)
        {
            throw new BusinessException(
                StatusCodes.Status400BadRequest,
                "Trang phải từ 1 đến 100000, số thông báo mỗi trang từ 1 đến 100.");
        }

        var userId = ActorId();

        // Chỉ lấy thông báo của người đang đăng nhập.
        var query = _context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        // Nếu được yêu cầu, chỉ lấy thông báo chưa đọc.
        if (unreadOnly)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        // Sắp xếp mới nhất trước, sau đó lấy trang được yêu cầu.
        var notifications = await query
            .OrderByDescending(notification => notification.SentAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(notification => new
            {
                notification.Id,
                notification.Title,
                notification.Content,
                notification.IsRead,
                notification.SentAt
            })
            .ToListAsync(cancellationToken);

        // Tổng số thông báo thỏa bộ lọc, tính trên tất cả các trang.
        var total = await query.CountAsync(cancellationToken);

        return Success(new
        {
            items = notifications,
            total,
            page,
            pageSize
        });
    }

    // 2. Đánh dấu một thông báo là đã đọc.
    // PATCH /api/notifications/5/read
    /// <summary>
    /// Đánh dấu một thông báo của tôi là đã đọc
    /// </summary>
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var userId = ActorId();

        // Chỉ được cập nhật thông báo thuộc tài khoản của mình.
        var notification = await _context.Notifications
            .SingleOrDefaultAsync(
                notification =>
                    notification.Id == id &&
                    notification.UserId == userId,
                cancellationToken);

        if (notification == null)
        {
            throw new BusinessException(
                StatusCodes.Status404NotFound,
                "Không tìm thấy thông báo của bạn.");
        }

        notification.IsRead = true;

        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}