using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Reports;

public class AuditLogQueryDTO
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    [Range(1, int.MaxValue)]
    public int? UserId { get; set; }

    [StringLength(50)]
    public string? Action { get; set; }

    [StringLength(100)]
    public string? EntityName { get; set; }

    [Range(1, int.MaxValue)]
    public int? EntityId { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public class AuditLogDTO
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string ActorName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public int? EntityId { get; set; }

    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}

public class AuditLogPageDTO
{
    public List<AuditLogDTO> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}