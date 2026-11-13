namespace SportsCenterAPI.Models
{
    /// <summary>
    /// Sport class scheduled by the center.
    /// Thực thể lớp học thể thao.
    /// </summary>
    public class SportClass
    {
        public int Id { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int SportId { get; set; }
        public Sport Sport { get; set; } = null!;
        public int? CoachId { get; set; }
        public Coach? Coach { get; set; }
        public decimal Price { get; set; }
        public int MaxCapacity { get; set; }
        public string? Schedule { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public string? ImageUrl { get; set; }

        // Navigation properties
        public ICollection<ClassRegistration> Registrations { get; set; } = new List<ClassRegistration>();
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}
