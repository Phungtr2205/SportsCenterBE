using SportsCenterAPI.DTOs.MembershipPackages;

namespace SportsCenterAPI.Services.Interface;

public interface IMembershipPackageService
{
    Task<IReadOnlyList<MembershipPackageDTO>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<MembershipPackageDTO> GetActiveByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MembershipPackageDTO>> GetForManagementAsync(int actorId, CancellationToken cancellationToken = default);
    Task<MembershipPackageDTO> GetForManagementByIdAsync(int id, int actorId, CancellationToken cancellationToken = default);
    Task<MembershipPackageDTO> CreateAsync(MembershipPackageRequestDTO request, int actorId, CancellationToken cancellationToken = default);
    Task<MembershipPackageDTO> UpdateAsync(int id, UpdateMembershipPackageRequestDTO request, int actorId, CancellationToken cancellationToken = default);
    Task<MembershipPackageDTO> SetImageAsync(int id, IFormFile file, int actorId, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, int actorId, CancellationToken cancellationToken = default);
}
