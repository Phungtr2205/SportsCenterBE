using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.MembershipPackages;

public class MembershipPackageRequestDTO
{
    [Required]
    [StringLength(200)]
    public string PackageName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PackageType { get; set; } // "Standard" hoặc "PT"

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Price { get; set; }

    [Range(1, 36500, ErrorMessage = "Duration must be between 1 and 36500 days (100 years).")]
    public int DurationInDays { get; set; }
}

public class UpdateMembershipPackageRequestDTO : MembershipPackageRequestDTO
{
    [Required]
    public bool? IsActive { get; set; }
}