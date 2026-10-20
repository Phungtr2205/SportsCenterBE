using SportsCenterAPI.DTOs.Attendances; 

namespace SportsCenterAPI.Services.Interface
{
    public interface IAttendanceService
    {
        Task<AttendanceResponseDTO> MarkAttendanceAsync(
            int actorId,
            MarkAttendanceDTO request, CancellationToken ct = default);

        Task<IEnumerable<AttendanceResponseDTO>> GetSessionAttendancesAsync(
            int actorId,
            int sessionId, CancellationToken ct = default);
    }
}
