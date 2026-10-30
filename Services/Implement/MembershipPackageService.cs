using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.MembershipPackages;
using SportsCenterAPI.Helpers;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class MembershipPackageService(AppDbContext context) : IMembershipPackageService
{
    public async Task<IReadOnlyList<MembershipPackageDTO>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var packages = await context.MembershipPackages.AsNoTracking()
            .Where(package => package.IsActive)
            .OrderBy(package => package.Id)
            .ToListAsync(cancellationToken);

        return packages.Select(ToDTO).ToList();
    }

    public async Task<MembershipPackageDTO> GetActiveByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var package = await context.MembershipPackages.AsNoTracking()
            .Where(package => package.Id == id && package.IsActive)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException(404, "Membership package not found.");

        return ToDTO(package);
    }

    public async Task<IReadOnlyList<MembershipPackageDTO>> GetForManagementAsync(int actorId, CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actorId, cancellationToken);
        var packages = await context.MembershipPackages.AsNoTracking()
            .OrderBy(package => package.Id)
            .ToListAsync(cancellationToken);

        return packages.Select(ToDTO).ToList();
    }

    public async Task<MembershipPackageDTO> GetForManagementByIdAsync(int id, int actorId, CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actorId, cancellationToken);
        var package = await context.MembershipPackages.AsNoTracking()
            .Where(package => package.Id == id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException(404, "Membership package not found.");

        return ToDTO(package);
    }

    public async Task<MembershipPackageDTO> CreateAsync(MembershipPackageRequestDTO request, int actorId, CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actorId, cancellationToken);
        Validate(request);

        var resolvedType = ResolvePackageType(request);

        var package = new MembershipPackage
        {
            PackageName = request.PackageName.Trim(),
            PackageType = resolvedType,
            Description = NormalizeDescription(request.Description),
            Price = request.Price,
            DurationInDays = request.DurationInDays,
            IsActive = true
        };

        context.MembershipPackages.Add(package);
        context.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = "Create",
            EntityName = nameof(MembershipPackage),
            Details = JsonSerializer.Serialize(new
            {
                package.PackageName,
                package.PackageType,
                package.Description,
                package.Price,
                package.DurationInDays,
                package.IsActive
            })
        });
        await context.SaveChangesAsync(cancellationToken);
        return ToDTO(package);
    }

    public async Task<MembershipPackageDTO> UpdateAsync(int id, UpdateMembershipPackageRequestDTO request, int actorId, CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actorId, cancellationToken);
        Validate(request);
        if (!request.IsActive.HasValue)
            throw new BusinessException(400, "IsActive is required when updating a membership package.");

        var package = await FindAsync(id, cancellationToken);
        var before = ToDTO(package);
        package.PackageName = request.PackageName.Trim();
        package.PackageType = ResolvePackageType(request);
        package.Description = NormalizeDescription(request.Description);
        package.Price = request.Price;
        package.DurationInDays = request.DurationInDays;
        package.IsActive = request.IsActive.Value;

        context.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = "Update",
            EntityName = nameof(MembershipPackage),
            EntityId = package.Id,
            Details = JsonSerializer.Serialize(new { Before = before, After = ToDTO(package) })
        });
        await context.SaveChangesAsync(cancellationToken);
        return ToDTO(package);
    }

    public async Task DeactivateAsync(int id, int actorId, CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actorId, cancellationToken);
        var package = await FindAsync(id, cancellationToken);
        if (!package.IsActive)
            return;

        package.IsActive = false;
        context.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = "Delete",
            EntityName = nameof(MembershipPackage),
            EntityId = package.Id,
            Details = JsonSerializer.Serialize(new { package.PackageName, IsActive = false, SoftDelete = true })
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<MembershipPackage> FindAsync(int id, CancellationToken cancellationToken)
    {
        return await context.MembershipPackages.SingleOrDefaultAsync(package => package.Id == id, cancellationToken)
            ?? throw new BusinessException(404, "Membership package not found.");
    }

    private async Task EnsureManagerAsync(int actorId, CancellationToken cancellationToken)
    {
        if (!await context.Users.AnyAsync(user => user.Id == actorId && user.IsActive && user.Role == "Manager", cancellationToken))
            throw new BusinessException(403, "An active Manager account is required.");
    }

    private static void Validate(MembershipPackageRequestDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.PackageName) || request.PackageName.Trim().Length > 200)
            throw new BusinessException(400, "Package name is required and must not exceed 200 characters.");
        if (request.Description?.Length > 2000)
            throw new BusinessException(400, "Description must not exceed 2000 characters.");
        if (request.Price <= 0 || request.Price > 9999999999999999.99m || decimal.Round(request.Price, 2) != request.Price)
            throw new BusinessException(400, "Price must be positive, fit decimal(18,2), and have at most two decimal places.");
        if (request.DurationInDays is <= 0 or > 36500)
            throw new BusinessException(400, "Duration must be between 1 and 36500 days (100 years).");
        if (!string.IsNullOrWhiteSpace(request.PackageType) &&
            !string.Equals(request.PackageType.Trim(), PackageTypeHelper.Standard, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.PackageType.Trim(), PackageTypeHelper.PT, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(400, "PackageType must be 'Standard' or 'PT'.");
        }
    }

    private static string ResolvePackageType(MembershipPackageRequestDTO request)
    {
        if (!string.IsNullOrWhiteSpace(request.PackageType))
        {
            return string.Equals(request.PackageType.Trim(), PackageTypeHelper.PT, StringComparison.OrdinalIgnoreCase)
                ? PackageTypeHelper.PT
                : PackageTypeHelper.Standard;
        }
        return PackageTypeHelper.GetPackageType(request.PackageName, request.Description);
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static MembershipPackageDTO ToDTO(MembershipPackage package) => new()
    {
        Id = package.Id,
        PackageName = package.PackageName,
        PackageType = PackageTypeHelper.GetPackageType(package),
        Description = package.Description,
        Price = package.Price,
        DurationInDays = package.DurationInDays,
        IsActive = package.IsActive
    };
}