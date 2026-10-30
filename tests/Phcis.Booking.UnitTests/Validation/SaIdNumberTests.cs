using Phcis.Booking.Shared.Validation;

namespace Phcis.Booking.UnitTests.Validation;

public class SaIdNumberTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    [Theory]
    [InlineData("8001015009087", 1980, 1, 1)]
    [InlineData("9506150123088", 1995, 6, 15)]
    [InlineData("0503205678181", 2005, 3, 20)]
    [InlineData("0002295001081", 2000, 2, 29)]
    public void Valid_ids_give_date_of_birth(string id, int year, int month, int day)
    {
        Assert.True(SaIdNumber.TryGetDateOfBirth(id, Today, out var dob));
        Assert.Equal(new DateOnly(year, month, day), dob);
    }

    [Fact]
    public void Two_digit_year_in_the_future_means_last_century()
    {
        // "24" is 2024 today, but would have been 1924 if checked in 2023.
        Assert.True(SaIdNumber.TryGetDateOfBirth("2401015001082", new DateOnly(2023, 6, 1), out var dob));
        Assert.Equal(new DateOnly(1924, 1, 1), dob);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("800101500908")]    // too short
    [InlineData("80010150090871")]  // too long
    [InlineData("800101500908A")]   // not numeric
    [InlineData("8001015009088")]   // wrong check digit
    [InlineData("8013015009082")]   // month 13 (check digit fixed up so only the date is wrong)
    [InlineData("8001015009384")]   // citizenship digit 3
    public void Invalid_ids_are_rejected(string? id)
    {
        Assert.False(SaIdNumber.IsValid(id, Today));
    }
}
