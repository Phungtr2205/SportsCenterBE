namespace SportsCenterAPI.DTOs.MembershipPackages;

public class MembershipPackageDTO
{
    public int Id { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string PackageType { get; set; } = "Standard";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationInDays { get; set; }
    public bool IsActive { get; set; }
}