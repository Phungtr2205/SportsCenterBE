namespace SportsCenterAPI.Helpers;

public static class PackageTypeHelper
{
    public const string Standard = "Standard";
    public const string PT = "PT";

    /// <summary>
    /// Nhận diện loại gói: PT (huấn luyện viên cá nhân) hay Standard (hội viên tập chung)
    /// </summary>
    public static string GetPackageType(string packageName, string? description = null)
    {
        if (packageName.Contains("PT", StringComparison.OrdinalIgnoreCase) ||
            (description != null && (
                description.Contains("PT", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("huấn luyện viên", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("1-1", StringComparison.OrdinalIgnoreCase)
            )))
        {
            return PT;
        }

        return Standard;
    }

    public static bool IsPt(string packageName, string? description = null) =>
        GetPackageType(packageName, description) == PT;
}