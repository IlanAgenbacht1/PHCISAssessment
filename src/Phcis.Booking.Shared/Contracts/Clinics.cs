namespace Phcis.Booking.Shared.Contracts;

public sealed record ClinicDto(int Id, string Name, string Address, string PhoneNumber);

/// <summary>Start and End carry the clinic's UTC offset, so the client shows clinic time whatever the browser's zone.</summary>
public sealed record TimeSlotDto(int Id, DateTimeOffset Start, DateTimeOffset End);
