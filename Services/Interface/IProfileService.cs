using SportsCenterAPI.DTOs.Profile;

namespace SportsCenterAPI.Services.Interface;

public interface IProfileService
{
    Task<ProfileDTO> GetAsync(int userId);

    Task<ProfileDTO> UpdateAsync(int userId, UpdateProfileDTO dto);

    Task SendChangeEmailOtpAsync(int userId, RequestChangeEmailOtpDTO dto);

    Task<ProfileDTO> VerifyChangeEmailOtpAsync(int userId, VerifyChangeEmailOtpDTO dto);
}