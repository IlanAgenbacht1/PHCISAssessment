namespace Phcis.Booking.Shared.Validation;

/// <summary>
/// South African ID number: YYMMDD SSSS C A Z.
/// Date of birth, 4-digit sequence (gender), citizenship (0 = citizen, 1 = permanent resident, 2 = refugee),
/// a legacy digit, and a Luhn check digit.
/// </summary>
public static class SaIdNumber
{
    public static bool IsValid(string? value, DateOnly today) => TryGetDateOfBirth(value, today, out _);

    public static bool TryGetDateOfBirth(string? value, DateOnly today, out DateOnly dateOfBirth)
    {
        dateOfBirth = default;

        if (value is not { Length: 13 } || !value.All(char.IsAsciiDigit)) return false;
        if (value[10] is not ('0' or '1' or '2')) return false;
        if (!PassesLuhn(value)) return false;

        var yy = int.Parse(value[..2]);
        var month = int.Parse(value[2..4]);
        var day = int.Parse(value[4..6]);

        // Two-digit year: assume this century unless that puts the birthday in the future.
        var year = 2000 + yy > today.Year ? 1900 + yy : 2000 + yy;
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;

        dateOfBirth = new DateOnly(year, month, day);
        if (dateOfBirth > today)
        {
            dateOfBirth = dateOfBirth.AddYears(-100);
        }

        return true;
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }
}
