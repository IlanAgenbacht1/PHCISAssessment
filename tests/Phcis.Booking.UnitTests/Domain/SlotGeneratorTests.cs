using Phcis.Booking.Domain.Scheduling;

namespace Phcis.Booking.UnitTests.Domain;

public class SlotGeneratorTests
{
    private static readonly TimeZoneInfo Johannesburg = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");

    private static readonly HashSet<DayOfWeek> Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    [Fact]
    public void Converts_clinic_local_times_to_utc()
    {
        // Monday 2 Nov 2026, 08:00 SAST is 06:00 UTC
        var template = new SlotTemplate(new(2026, 11, 2), new(2026, 11, 2), new(8, 0), new(9, 0), 30, Weekdays);

        var slots = SlotGenerator.Generate(template, Johannesburg);

        Assert.Equal(
            [
                new SlotWindow(Utc(2026, 11, 2, 6, 0), Utc(2026, 11, 2, 6, 30)),
                new SlotWindow(Utc(2026, 11, 2, 6, 30), Utc(2026, 11, 2, 7, 0))
            ],
            slots);
    }

    [Fact]
    public void Skips_days_not_in_template()
    {
        // Fri 6 Nov to Mon 9 Nov: only Friday and Monday
        var template = new SlotTemplate(new(2026, 11, 6), new(2026, 11, 9), new(8, 0), new(8, 20), 20, Weekdays);

        var slots = SlotGenerator.Generate(template, Johannesburg);

        Assert.Equal([6, 9], slots.Select(s => s.StartUtc.Day));
    }

    [Fact]
    public void Drops_trailing_partial_slot()
    {
        // 08:00 to 09:00 in 25 minute slots: 08:00, 08:25. 08:50 would run past 09:00.
        var template = new SlotTemplate(new(2026, 11, 2), new(2026, 11, 2), new(8, 0), new(9, 0), 25, Weekdays);

        var slots = SlotGenerator.Generate(template, Johannesburg);

        Assert.Equal(2, slots.Count);
        Assert.All(slots, s => Assert.True(s.EndUtc <= Utc(2026, 11, 2, 7, 0)));
    }

    [Fact]
    public void Skips_local_times_that_do_not_exist()
    {
        // London springs forward at 01:00 on 29 March 2026, so 01:00 to 01:59 local never happens.
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var template = new SlotTemplate(new(2026, 3, 29), new(2026, 3, 29), new(0, 0), new(3, 0), 60, new HashSet<DayOfWeek> { DayOfWeek.Sunday });

        var slots = SlotGenerator.Generate(template, london);

        Assert.Equal(2, slots.Count); // 00:00 and 02:00, no 01:00
    }

    [Fact]
    public void Rejects_day_that_ends_before_it_starts()
    {
        var template = new SlotTemplate(new(2026, 11, 2), new(2026, 11, 2), new(9, 0), new(8, 0), 20, Weekdays);

        Assert.Throws<ArgumentException>(() => SlotGenerator.Generate(template, Johannesburg));
    }

    private static DateTime Utc(int y, int m, int d, int h, int min) => new(y, m, d, h, min, 0, DateTimeKind.Utc);
}
