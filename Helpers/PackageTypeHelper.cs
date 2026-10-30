using System.Text.RegularExpressions;
using SportsCenterAPI.Models;

namespace SportsCenterAPI.Helpers;

public static partial class PackageTypeHelper
{
    public const string Standard = "Standard";
    public const string PT = "PT";

    [GeneratedRegex(@"\bPT\b", RegexOptions.IgnoreCase)]
    private static partial Regex PtWordRegex();

    public static string GetPackageType(MembershipPackage? package)
    {
        if (package == null) return Standard;
        if (string.Equals(package.PackageType, PT, StringComparison.OrdinalIgnoreCase))
            return PT;

        return GetPackageType(package.PackageName, package.Description);
    }

    public static string GetPackageType(string? packageName, string? description = null)
    {
        if (!string.IsNullOrWhiteSpace(packageName) &&
            (PtWordRegex().IsMatch(packageName) ||
             packageName.Contains("1-1", StringComparison.OrdinalIgnoreCase) ||
             packageName.Contains("Personal Trainer", StringComparison.OrdinalIgnoreCase)))
        {
            return PT;
        }

        if (!string.IsNullOrWhiteSpace(description) &&
            (PtWordRegex().IsMatch(description) ||
             description.Contains("huấn luyện viên", StringComparison.OrdinalIgnoreCase) ||
             description.Contains("Personal Trainer", StringComparison.OrdinalIgnoreCase) ||
             description.Contains("1-1", StringComparison.OrdinalIgnoreCase)))
        {
            return PT;
        }

        return Standard;
    }

    public static bool IsPt(string? packageName, string? description = null) =>
        GetPackageType(packageName, description) == PT;
}