namespace SportsCenterAPI.Models
{
    /// <summary>
    /// Member profile entity linked 1-to-1 with User.
    /// Thực thể hồ sơ hội viên liên kết 1-1 với tài khoản User.
    /// </summary>
    public class Member
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime? DateOfBirth { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(10)]
        public string? Gender { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string? FitnessGoal { get; set; }
        public string? EmergencyContact { get; set; }
        public string? Address { get; set; }
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<MemberSubscription> Subscriptions { get; set; } = new List<MemberSubscription>();
        public ICollection<ClassRegistration> Registrations { get; set; } = new List<ClassRegistration>();
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<TrainingPlan> TrainingPlans { get; set; } = new List<TrainingPlan>();
        public ICollection<CoachReview> CoachReviews { get; set; } = new List<CoachReview>();
    }
}
