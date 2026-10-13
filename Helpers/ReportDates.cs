namespace SportsCenterAPI.Helpers;

public static class ReportDates
{
    public static DateOnly Today()
    {
        return DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    }

    public static DateTime StartUtc(DateOnly date)
    {
        var localMidnight = date.ToDateTime(TimeOnly.MinValue);

        return DateTime.SpecifyKind(
            localMidnight.AddHours(-7),
            DateTimeKind.Utc);
    }

    public static void Validate(DateOnly from, DateOnly to)
    {
        if (from.Year < 2000 || to.Year > 2100 || from > to)
        {
            throw new BusinessException(
                400,
                "Khoảng ngày không hợp lệ. Dùng năm từ 2000 đến 2100 và From <= To.");
        }

        if (to.DayNumber - from.DayNumber > 365)
        {
            throw new BusinessException(
                400,
                "Mỗi lần xem tối đa 366 ngày.");
        }
    }

    public static void ValidateOptional(
        DateOnly? from,
        DateOnly? to)
    {
        if (from.HasValue != to.HasValue)
        {
            throw new BusinessException(
                400,
                "Cần truyền cả from và to.");
        }

        if (from.HasValue && to.HasValue)
        {
            Validate(from.Value, to.Value);
        }
    }
}