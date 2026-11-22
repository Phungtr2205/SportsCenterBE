using System.ComponentModel.DataAnnotations;

namespace SportsCenterAPI.Helpers;

/// <summary>Ngày sinh từ năm 1950 và đủ 16 tuổi trở lên.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class BirthDateAttribute : ValidationAttribute
{
    private const int MinAge = 16;

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not DateTime d) return ValidationResult.Success;

        var members = new[] { context.MemberName! };
        var today = DateTime.UtcNow.Date;

        if (d.Date > today)
            return new ValidationResult("Ngày sinh không được ở tương lai.", members);
        if (d.Year < 1950)
            return new ValidationResult("Năm sinh không hợp lệ, vui lòng kiểm tra lại.", members);
        if (d.Date > today.AddYears(-MinAge))
            return new ValidationResult($"Bạn cần đủ {MinAge} tuổi để đăng ký.", members);

        return ValidationResult.Success;
    }
}
