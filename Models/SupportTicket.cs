using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.Models
{
    public class SupportTicket
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        [MaxLength(50)]
        public string Status { get; set; } = "Open"; // Open, InProgress, Resolved

        [MaxLength(50)]
        public string Priority { get; set; } = "Normal"; // Low, Normal, High

        // Người gửi yêu cầu (Hội viên)
        public int MemberId { get; set; }
        public Member Member { get; set; } = null!;

        // Người xử lý (Lễ tân / Quản lý)
        public int? HandledByUserId { get; set; }
        public User? HandledByUser { get; set; }

        public string? ResolutionNote { get; set; } // Ghi chú phản hồi

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; } // Ảnh mô tả chi tiết sự cố
    }
}