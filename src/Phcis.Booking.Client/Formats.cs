namespace Phcis.Booking.Client;

/// <summary>
/// Appointment times arrive with the clinic's UTC offset attached. Formatting the DateTimeOffset as-is
/// shows clinic time; calling ToLocalTime() would quietly switch to the browser's zone.
/// </summary>
public static class Formats
{
    public static string Time(this DateTimeOffset value) => value.ToString("HH:mm");

    public static string DayAndTime(this DateTimeOffset value) => value.ToString("ddd d MMM, HH:mm");

    public static string LongDate(this DateTimeOffset value) => value.ToString("dddd d MMMM yyyy");

    public static string Day(this DateOnly value) => value.ToString("ddd d MMM");
}
