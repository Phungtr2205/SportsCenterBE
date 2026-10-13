using SportsCenterAPI.DTOs.Rooms;

namespace SportsCenterAPI.Services.Interface;

public interface IRoomService
{
    Task<IEnumerable<RoomResponse>> GetAllRoomsAsync(bool? isActive = null);
    Task<RoomResponse> GetRoomByIdAsync(int id);
    Task<RoomResponse> CreateRoomAsync(CreateRoomRequest request);
    Task<RoomResponse> UpdateRoomAsync(int id, UpdateRoomRequest request);
    Task DeleteRoomAsync(int id);
    Task<bool> IsRoomAvailableAsync(int roomId, DateTime startsAt, DateTime endsAt, int? excludeSessionId = null);
}