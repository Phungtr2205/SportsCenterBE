using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.ClassCatalog;

public class SportRequestDTO
{
    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class CoachProfileRequestDTO
{
    [StringLength(200)]
    public string? Specialization { get; set; }

    [StringLength(2000)]
    public string? Bio { get; set; }

    [Range(0, 80)]
    public int YearsOfExperience { get; set; }
}

public class SportListDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
}

public class SportDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
}

public class CoachDTO
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string? Bio { get; set; }
    public int YearsOfExperience { get; set; }
}

public class CoachProfileDTO
{
    public int Id { get; set; }
    public string? Specialization { get; set; }
    public string? Bio { get; set; }
    public int YearsOfExperience { get; set; }
}