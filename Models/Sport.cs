namespace SportsCenterAPI.Models
{
    /// <summary>
    /// Sport discipline entity (e.g., Gym, Yoga, Swimming, Boxing).
    /// Thực thể bộ môn thể thao.
    /// </summary>
    public class Sport
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<SportClass> Classes { get; set; } = new List<SportClass>();
    }
}
