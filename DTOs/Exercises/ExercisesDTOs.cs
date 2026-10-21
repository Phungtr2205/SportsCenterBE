using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.DTOs.Exercises;

public class ExerciseResponseDTO
{
    public int Id { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public string? MuscleGroup { get; set; }
    public string? Description { get; set; }
    public string? VideoUrl { get; set; }
}

public class CreateExerciseDTO
{
    [Required(ErrorMessage = "Tên bài tập không được để trống.")]
    [StringLength(150, ErrorMessage = "Tên bài tập không vượt quá 150 ký tự.")]
    public string ExerciseName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? MuscleGroup { get; set; } // Legs, Chest, Back, Arms, Core, Cardio, FullBody

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? VideoUrl { get; set; }
}

public class UpdateExerciseDTO
{
    [Required(ErrorMessage = "Tên bài tập không được để trống.")]
    [StringLength(150, ErrorMessage = "Tên bài tập không vượt quá 150 ký tự.")]
    public string ExerciseName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? MuscleGroup { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? VideoUrl { get; set; }
}