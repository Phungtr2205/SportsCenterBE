using SportsCenterAPI.DTOs.Attendances; 

namespace SportsCenterAPI.Services.Interface
{
    public interface IAttendanceService
    {
        Task<AttendanceResponseDTO> MarkAttendanceAsync(MarkAttendanceDTO request);
        Task<IEnumerable<AttendanceResponseDTO>> GetSessionAttendancesAsync(int sessionId);
    }
}
