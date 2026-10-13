using SportsCenterAPI.DTOs.CheckIn;

namespace SportsCenterAPI.Services.Interface;

public interface IGymCheckInService
{
    Task<GymCheckInResponse> CheckInMemberAsync(int staffUserId, CheckInRequest request);
    Task<GymCheckInResponse> ScanQrCheckInAsync(int staffUserId, ScanQrRequest request);
    Task<MemberQrResponse> GetMyQrCodeAsync(int userId);
    Task<GymCheckInResponse> CheckOutMemberAsync(int checkInId);
    Task<IEnumerable<GymCheckInResponse>> GetTodayCheckInsAsync();
    Task<IEnumerable<GymCheckInResponse>> GetMyHistoryAsync(int userId);
}