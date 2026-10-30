namespace Phcis.Booking.Shared.Contracts;

public sealed record DayListDto(
    int ClinicId,
    string ClinicName,
    DateOnly Date,
    int TotalSlots,
    int BookedSlots,
    IReadOnlyList<DayListEntryDto> Appointments);

public sealed record DayListEntryDto(
    int AppointmentId,
    string Reference,
    DateTimeOffset Start,
    string PatientName,
    string MaskedIdentityNumber,
    string PhoneNumber,
    AppointmentStatus Status,
    string? ReasonForVisit,
    bool CanRecordAttendance);

public sealed class RecordAttendanceRequest
{
    public bool Attended { get; set; }
}

public sealed class GenerateSlotsRequest
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public TimeOnly DayStart { get; set; } = new(8, 0);
    public TimeOnly DayEnd { get; set; } = new(16, 0);
    public int SlotMinutes { get; set; } = 20;
    public List<DayOfWeek> Days { get; set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
}

public sealed record GenerateSlotsResult(int Created, int AlreadyExisted);
