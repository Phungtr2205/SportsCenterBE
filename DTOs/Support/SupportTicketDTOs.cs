using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Support
{
    public class CreateTicketRequest
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = null!;

        [Required, StringLength(4000)]
        public string Description { get; set; } = null!;

        [RegularExpression("^(Low|Normal|High)$", ErrorMessage = "Priority phải là Low, Normal hoặc High.")]
        public string Priority { get; set; } = "Normal";
    }

    public class ResolveTicketRequest
    {
        [Required]
        [RegularExpression("^(Open|InProgress|Resolved|Closed)$", ErrorMessage = "Status phải là Open, InProgress, Resolved hoặc Closed.")]
        public string Status { get; set; } = null!;

        [StringLength(2000)]
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