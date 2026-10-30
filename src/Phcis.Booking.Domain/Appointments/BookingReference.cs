using System.Security.Cryptography;

namespace Phcis.Booking.Domain.Appointments;

public static class BookingReference
{
    // No 0/O or 1/I: these get read out over the phone and at reception.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string New() => "PHC-" + new string(RandomNumberGenerator.GetItems<char>(Alphabet, 6));
}
