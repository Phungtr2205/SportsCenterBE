using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Reports;
using SportsCenterAPI.Helpers;

namespace SportsCenterAPI.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = UserRoles.Manager)]
public class AuditLogsController(AppDbContext context) : BaseApiController
{
    /// <summary>
    /// Xem lịch sử thao tác hệ thống, có bộ lọc và phân trang
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] AuditLogQueryDTO request,
        CancellationToken ct)
    {
        // 1. Kiểm tra khoảng ngày tìm kiếm.
        if (request.From.HasValue && request.To.HasValue &&
            request.From.Value > request.To.Value)
        {
            throw new BusinessException(
                400,
                "Ngày bắt đầu không được sau ngày kết thúc.");
        }

        // 2. Kiểm tra tài khoản vẫn hoạt động và còn quyền Manager.
        var actorId = ActorId();

        var isManager = await context.Users.AnyAsync(
            u => u.Id == actorId &&
                 u.IsActive &&
                 u.Role == UserRoles.Manager,
            ct);

        if (!isManager)
        {
            throw new BusinessException(
                403,
                "Chỉ Manager đang hoạt động được xem nhật ký.");
        }

        // 3. Tạo truy vấn chỉ đọc.
        var query = context.AuditLogs.AsNoTracking();

        // Các ngày lọc được tính theo UTC như Timestamp trong DB.
        if (request.From.HasValue)
        {
            var from = request.From.Value.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc);

            query = query.Where(log => log.Timestamp >= from);
        }

        if (request.To.HasValue &&
            request.To.Value != DateOnly.MaxValue)
        {
            // Lấy hết ngày To: lọc trước 00:00 ngày kế tiếp.
            var until = request.To.Value.AddDays(1).ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc);

            query = query.Where(log => log.Timestamp < until);
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(
                log => log.UserId == request.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();

            query = query.Where(log => log.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            var entityName = request.EntityName.Trim();

            query = query.Where(
                log => log.EntityName == entityName);
        }

        if (request.EntityId.HasValue)
        {
            query = query.Where(
                log => log.EntityId == request.EntityId.Value);
        }

        // 4. Đếm tổng kết quả trước khi phân trang.
        var total = await query.CountAsync(ct);

        // 5. Lấy nhật ký mới nhất trước.
        var items = await query
            .OrderByDescending(log => log.Timestamp)
            .ThenByDescending(log => log.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(log => new AuditLogDTO
            {
                Id = log.Id,
                UserId = log.UserId,
                ActorName = log.User != null
                    ? log.User.FullName
                    : "Hệ thống",
                Action = log.Action,
                EntityName = log.EntityName,
                EntityId = log.EntityId,
                Details = log.Details,
                Timestamp = log.Timestamp
            })
            .ToListAsync(ct);

        // 6. Trả danh sách cùng thông tin phân trang.
        return Success(
            new AuditLogPageDTO
            {
                Items = items,
                Total = total,
                Page = request.Page,
                PageSize = request.PageSize
            },
            "Lấy lịch sử thao tác thành công.");
    }
}