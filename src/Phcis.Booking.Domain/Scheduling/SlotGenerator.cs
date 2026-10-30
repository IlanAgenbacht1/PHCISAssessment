namespace Phcis.Booking.Domain.Scheduling;

public sealed record SlotTemplate(
    DateOnly From,
    DateOnly To,
    TimeOnly DayStart,
    TimeOnly DayEnd,
    int SlotMinutes,
    IReadOnlySet<DayOfWeek> Days);

public readonly record struct SlotWindow(DateTime StartUtc, DateTime EndUtc);

public static class SlotGenerator
{
    /// <summary>
    /// Expands a template into UTC slot windows. Times in the template are clinic-local.
    /// A slot that wouldn't finish by <see cref="SlotTemplate.DayEnd"/> is dropped instead of being cut short.
    /// </summary>
    public static IReadOnlyList<SlotWindow> Generate(SlotTemplate template, TimeZoneInfo clinicTimeZone)
    {
        if (template.SlotMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(template), "Slot length must be positive.");
        if (template.DayEnd <= template.DayStart) throw new ArgumentException("Day must end after it starts.", nameof(template));

        var length = TimeSpan.FromMinutes(template.SlotMinutes);
        var windows = new List<SlotWindow>();

        for (var date = template.From; date <= template.To; date = date.AddDays(1))
        {
            if (!template.Days.Contains(date.DayOfWeek)) continue;

            var dayEnd = date.ToDateTime(template.DayEnd);
            for (var start = date.ToDateTime(template.DayStart); start + length <= dayEnd; start += length)
            {
                // SA has no DST, but the clinic's zone is data, so don't assume. Skip times that don't
                // exist locally (spring-forward gap) instead of letting ConvertTimeToUtc throw.
                if (clinicTimeZone.IsInvalidTime(start)) continue;

                var startUtc = TimeZoneInfo.ConvertTimeToUtc(start, clinicTimeZone);
                windows.Add(new SlotWindow(startUtc, startUtc + length));
            }
        }

        return windows;
    }
}
