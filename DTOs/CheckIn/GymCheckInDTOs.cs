using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.CheckIn;

public class CheckInRequest : IValidatableObject
{
    // Có thể nhập Mã hội viên HOẶC Số điện thoại
    [Range(1, int.MaxValue)]
    public int? MemberId { get; set; }
    [StringLength(30)]
    public string? PhoneNumber { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!MemberId.HasValue && string.IsNullOrWhiteSpace(PhoneNumber))
            yield return new ValidationResult("Cần nhập mã hội viên hoặc số điện thoại.", [nameof(MemberId)]);
    }
}

public class ScanQrRequest
{
    [Required, StringLength(200)]
    public string QrCode { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Notes { get; set; }
}

public class MemberQrResponse
{
    public int MemberId { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string QrData { get; set; } = string.Empty; // Chuỗi dùng để vẽ mã QR
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? PackageName { get; set; }
    public string PackageStatus { get; set; } = string.Empty; // Active, Expired, None
    public DateTime? ExpireDate { get; set; }
}

public class GymCheckInResponse
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public DateTime CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public int? DurationMinutes { get; set; }
    /// InGym (Đang tập hôm nay), CheckedOut (Đã ra về), ForgotCheckOut (Qua ngày chưa check-out)
    public string Status { get; set; } = string.Empty;
    public string? CheckedByStaffName { get; set; }
    public string? Notes { get; set; }
}

public class GymCheckInFilterRequest : IValidatableObject
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [Range(1, int.MaxValue)]
    public int? MemberId { get; set; }
    [StringLength(100)]
    public string? Search { get; set; } // Tìm theo Tên, SĐT hoặc Mã SC-MEM-...
    [RegularExpression("^(All|InGym|CheckedOut|ForgotCheckOut)$", ErrorMessage = "Status không hợp lệ.")]
    public string? Status { get; set; } // All, InGym, CheckedOut, ForgotCheckOut

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date > ToDate.Value.Date)
            yield return new ValidationResult("FromDate không được sau ToDate.", [nameof(FromDate)]);
    }
}

public class MemberCheckInSummaryDTO
{
    public int MemberId { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string CurrentPackageName { get; set; } = string.Empty;
    public int TotalCheckIns { get; set; }
    public int TotalDurationMinutes { get; set; }
    public DateTime LastCheckInTime { get; set; }
    public bool IsCurrentlyInGym { get; set; }
}