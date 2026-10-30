namespace Phcis.Booking.Domain.Clinics;

public class Clinic
{
    private Clinic() { }

    public Clinic(string name, string address, string phoneNumber, string timeZoneId)
    {
        Name = name;
        Address = address;
        PhoneNumber = phoneNumber;
        TimeZoneId = timeZoneId;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;

    /// <summary>IANA id, e.g. "Africa/Johannesburg". Slots are stored in UTC and shown in clinic time.</summary>
    public string TimeZoneId { get; private set; } = null!;

    public TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
}
