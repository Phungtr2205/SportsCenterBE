using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.Models;

/// <summary>
/// Thực thể Phòng tập / Không gian tập luyện tại trung tâm
/// </summary>
public class Room
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty; // VD: Studio Yoga 01, Sân Boxing Tầng 2

    [MaxLength(50)]
    public string RoomType { get; set; } = "General"; // Gym, Yoga, Boxing, Swimming, General

    public int Capacity { get; set; } = 30; // Sức chứa tối đa

    [MaxLength(100)]
    public string? Location { get; set; } // VD: Tầng 2 - Khu B

    public bool IsActive { get; set; } = true;

    // Quan hệ: 1 phòng có thể diễn ra nhiều buổi học
    public ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
}