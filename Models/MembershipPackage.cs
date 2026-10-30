using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.Models
{
    /// <summary>
    /// Membership package offering (e.g., Monthly, Quarterly, Annual, VIP).
    /// Thực thể gói tập hội viên.
    /// </summary>
    public class MembershipPackage
    {
        public int Id { get; set; }
        public string PackageName { get; set; } = string.Empty;
        [MaxLength(20)]
        public string PackageType { get; set; } = "Standard"; // Standard hoặc PT
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DurationInDays { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<MemberSubscription> Subscriptions { get; set; } = new List<MemberSubscription>();
    }
}