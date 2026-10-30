namespace Phcis.Booking.Domain;

/// <summary>
/// Thrown when a request is well-formed but breaks a booking rule
/// (e.g. cancelling an appointment that has already started).
/// </summary>
public class BookingRuleException(string message) : Exception(message);

/// <summary>
/// The slot was taken by someone else. Separate type because the API returns 409 for this,
/// and the client reacts to it differently (refreshes the slot list).
/// </summary>
public sealed class SlotUnavailableException()
    : BookingRuleException("Sorry, that slot has just been taken. Please choose another time.");
