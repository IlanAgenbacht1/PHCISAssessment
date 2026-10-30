namespace Phcis.Booking.IntegrationTests;

internal static class TestTimes
{
    /// <summary>
    /// 09:00 SAST, a few days out, on the minute. Each call gets a different day so tests sharing a
    /// patient's calendar never overlap by accident.
    /// </summary>
    public static DateTime FutureMorningUtc(int daysAhead) =>
        DateTime.UtcNow.Date.AddDays(daysAhead).AddHours(7);

    public static DateOnly ClinicDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg")));
}
