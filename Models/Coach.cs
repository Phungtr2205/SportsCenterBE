namespace SportsCenterAPI.Models
{
    /// <summary>
    /// Coach/Trainer profile entity linked 1-to-1 with User.
    /// Thực thể hồ sơ huấn luyện viên liên kết 1-1 với tài khoản User.
    /// </summary>
    public class Coach
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string? Specialization { get; set; }
        public string? Bio { get; set; }
        public int YearsOfExperience { get; set; }

        // Navigation properties
        public ICollection<SportClass> Classes { get; set; } = new List<SportClass>();
        public ICollection<TrainingPlan> TrainingPlans { get; set; } = new List<TrainingPlan>();
    }
}
