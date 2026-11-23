using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Attendances
{
    public class MarkAttendanceDTO
    {
        [Range(1, int.MaxValue)]
        public int MemberId { get; set; }

        [Range(1, int.MaxValue)]
        public int SessionId { get; set; }
        [Required]
        [RegularExpression("^(Present|Absent|Late|Excused)$",ErrorMessage = "Status phải là Present, Absent, Late hoặc Excused.")]
        public string Status { get; set; } = "Present";
    }

    public class AttendanceResponseDTO
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? SessionId { get; set; }
    }
}
