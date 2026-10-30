namespace Phcis.Booking.Api.Infrastructure;

internal static class ClinicTime
{
    public static DateTimeOffset ToClinicTime(this DateTime utc, TimeZoneInfo clinicZone) =>
        TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), clinicZone);

    /// <summary>UTC range covering a calendar day in the clinic's time zone, end exclusive.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) DayBoundsUtc(DateOnly date, TimeZoneInfo clinicZone) =>
        (TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), clinicZone),
         TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), clinicZone));

    public static DateOnly Today(this TimeProvider time, TimeZoneInfo clinicZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), clinicZone).DateTime);
}
