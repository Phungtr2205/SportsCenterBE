using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Rooms;

public class CreateRoomRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(50)]
    public string RoomType { get; set; } = "General";

    [Range(1, 500)]
    public int Capacity { get; set; } = 30;

    [MaxLength(100)]
    public string? Location { get; set; }
}

public class UpdateRoomRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(50)]
    public string RoomType { get; set; } = "General";

    [Range(1, 500)]
    public int Capacity { get; set; } = 30;

    [MaxLength(100)]
    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;
}

public class RoomResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; }
}