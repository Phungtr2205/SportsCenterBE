using Microsoft.EntityFrameworkCore;
using SportsCenterAPI.Data;
using SportsCenterAPI.DTOs.Rooms;
using SportsCenterAPI.Models;
using SportsCenterAPI.Services.Interface;

namespace SportsCenterAPI.Services.Implement;

public class RoomService : IRoomService
{
    private readonly AppDbContext _context;

    public RoomService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<RoomResponse>> GetAllRoomsAsync(bool? isActive = null)
    {
        var query = _context.Rooms.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(r => r.IsActive == isActive.Value);

        return await query.OrderBy(r => r.Name)
            .Select(r => MapToResponse(r))
            .ToListAsync();
    }

    public async Task<RoomResponse> GetRoomByIdAsync(int id)
    {
        var room = await _context.Rooms.FindAsync(id)
            ?? throw new KeyNotFoundException("Không tìm thấy phòng tập.");

        return MapToResponse(room);
    }

    public async Task<RoomResponse> CreateRoomAsync(CreateRoomRequest request)
    {
        var exists = await _context.Rooms.AnyAsync(r => r.Name.ToLower() == request.Name.Trim().ToLower());
        if (exists)
            throw new InvalidOperationException($"Phòng với tên '{request.Name}' đã tồn tại.");

        var room = new Room
        {
            Name = request.Name.Trim(),
            RoomType = request.RoomType,
            Capacity = request.Capacity,
            Location = request.Location,
            IsActive = true
        };

        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();

        return MapToResponse(room);
    }

    public async Task<RoomResponse> UpdateRoomAsync(int id, UpdateRoomRequest request)
    {
        var room = await _context.Rooms.FindAsync(id)
            ?? throw new KeyNotFoundException("Không tìm thấy phòng tập.");

        var duplicateName = await _context.Rooms
            .AnyAsync(r => r.Id != id && r.Name.ToLower() == request.Name.Trim().ToLower());
        if (duplicateName)
            throw new InvalidOperationException($"Tên phòng '{request.Name}' đã được sử dụng bởi phòng khác.");

        room.Name = request.Name.Trim();
        room.RoomType = request.RoomType;
        room.Capacity = request.Capacity;
        room.Location = request.Location;
        room.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        return MapToResponse(room);
    }

    public async Task DeleteRoomAsync(int id)
    {
        var room = await _context.Rooms.FindAsync(id)
            ?? throw new KeyNotFoundException("Không tìm thấy phòng tập.");

        // Kiểm tra xem phòng có đang được dùng cho các buổi học trong tương lai không
        var hasFutureSessions = await _context.ClassSessions
            .AnyAsync(s => s.RoomId == id && s.StartsAt >= DateTime.UtcNow && s.Status != "Cancelled");

        if (hasFutureSessions)
            throw new InvalidOperationException("Không thể xóa hoặc vô hiệu hóa phòng đang có lịch học trong tương lai.");

        // Soft delete (chuyển sang ngưng hoạt động) để giữ dữ liệu lịch sử
        room.IsActive = false;
        await _context.SaveChangesAsync();
    }

    // Thuật toán kiểm tra chống trùng lịch học tại cùng 1 phòng
    public async Task<bool> IsRoomAvailableAsync(int roomId, DateTime startsAt, DateTime endsAt, int? excludeSessionId = null)
    {
        // 2 khung giờ trùng nhau khi: startsAt < s.EndsAt && endsAt > s.StartsAt
        var isOccupied = await _context.ClassSessions
            .AnyAsync(s => s.RoomId == roomId
                        && s.Status != "Cancelled"
                        && (excludeSessionId == null || s.Id != excludeSessionId)
                        && startsAt < s.EndsAt
                        && endsAt > s.StartsAt);

        return !isOccupied; // Trả về true nếu phòng còn trống
    }

    private static RoomResponse MapToResponse(Room r)
    {
        return new RoomResponse
        {
            Id = r.Id,
            Name = r.Name,
            RoomType = r.RoomType,
            Capacity = r.Capacity,
            Location = r.Location,
            IsActive = r.IsActive
        };
    }
}