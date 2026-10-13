using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.CheckIn;

public class CheckInRequest
{
    // Có thể nhập Mã hội viên HOẶC Số điện thoại
    public int? MemberId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Notes { get; set; }
}

public class ScanQrRequest
{
    [Required]
    public string QrCode { get; set; } = string.Empty;
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
    public string? CheckedByStaffName { get; set; }
    public string? Notes { get; set; }
}