using SportsCenterAPI.DTOs.Accounts;

namespace SportsCenterAPI.Services.Interface;

public interface IAccountService
{
    Task<IReadOnlyList<MemberResponseDTO>> GetMembersAsync();
    Task<MemberResponseDTO> GetMemberAsync(int memberId);
    Task<MemberResponseDTO> GetMyMemberAsync(int userId);
    Task DeactivateMemberAsync(int memberId,int callerUserId);
    Task<IReadOnlyList<StaffResponseDTO>> GetStaffAsync();
    Task<StaffResponseDTO> GetStaffAsync(int userId);
    Task<StaffResponseDTO> CreateStaffAsync(CreateStaffRequestDTO request, int callerUserId);
    Task<StaffResponseDTO> UpdateStaffRoleAsync(int userId,UpdateStaffRoleRequestDTO request,int callerUserId);
    Task DeactivateStaffAsync(int userId,int callerUserId);
}