using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Support
{
    public class CreateTicketRequest
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        [MaxLength(50)]
        public string Priority { get; set; } = "Normal";
    }

    public class ResolveTicketRequest
    {
        [Required]
        public string Status { get; set; } = null!;

        public string? ResolutionNote { get; set; }
    }

    public class SupportTicketResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string Priority { get; set; } = null!;
        public string MemberName { get; set; } = null!;
        public string? HandledByUserName { get; set; }
        public string? ResolutionNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ImageUrl { get; set; }
    }
}