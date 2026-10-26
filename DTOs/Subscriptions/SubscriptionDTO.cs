namespace SportsCenterAPI.DTOs.Subscriptions;

public class SubscriptionDTO
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public decimal AgreedPrice { get; set; }
    public int DurationInDays { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<PaymentReceiptDTO> Payments { get; set; } = [];
}

public class PaymentReceiptDTO
{
    public int Id { get; set; }
    public int? SubscriptionId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string? TransactionReference { get; set; }
}

public class UpgradePreviewDTO
{
    public bool CanUpgrade { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? CurrentSubscriptionId { get; set; }
    public string? CurrentPackageName { get; set; }
    public decimal CurrentAgreedPrice { get; set; }
    public int TargetPackageId { get; set; }
    public string TargetPackageName { get; set; } = string.Empty;
    public decimal TargetPackagePrice { get; set; }
    public decimal DiscountAmount { get; set; } // Số tiền gói cũ được trừ
    public decimal AmountToPay { get; set; }    // Số tiền chênh lệch cần trả
    public DateTime? NewStartDate { get; set; }
    public DateTime? NewEndDate { get; set; }
}