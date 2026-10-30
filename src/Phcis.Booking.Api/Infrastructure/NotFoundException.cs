namespace Phcis.Booking.Api.Infrastructure;

/// <summary>
/// Also used when a record exists but belongs to someone else, so callers can't probe for other
/// patients' booking references.
/// </summary>
public sealed class NotFoundException(string what) : Exception($"{what} was not found.");
