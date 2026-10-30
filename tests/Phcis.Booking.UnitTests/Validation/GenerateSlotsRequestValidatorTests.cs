using Microsoft.Extensions.Time.Testing;
using Phcis.Booking.Shared.Contracts;
using Phcis.Booking.Shared.Validation;

namespace Phcis.Booking.UnitTests.Validation;

public class GenerateSlotsRequestValidatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    private readonly GenerateSlotsRequestValidator _validator =
        new(new FakeTimeProvider(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void Defaults_for_next_week_are_valid()
    {
        var request = new GenerateSlotsRequest { From = Today.AddDays(3), To = Today.AddDays(7) };

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_past_start_date()
    {
        var request = new GenerateSlotsRequest { From = Today.AddDays(-1), To = Today };

        Assert.Contains(_validator.Validate(request).Errors, e => e.PropertyName == nameof(request.From));
    }

    [Fact]
    public void Rejects_range_longer_than_limit()
    {
        var request = new GenerateSlotsRequest
        {
            From = Today,
            To = Today.AddDays(GenerateSlotsRequestValidator.MaxDaysAhead)
        };

        Assert.Contains(_validator.Validate(request).Errors, e => e.PropertyName == nameof(request.To));
    }

    [Fact]
    public void Rejects_day_shorter_than_one_slot()
    {
        var request = new GenerateSlotsRequest
        {
            From = Today,
            To = Today,
            DayStart = new TimeOnly(8, 0),
            DayEnd = new TimeOnly(8, 15),
            SlotMinutes = 20
        };

        Assert.Contains(_validator.Validate(request).Errors, e => e.ErrorMessage.Contains("shorter than one slot"));
    }

    [Fact]
    public void Requires_at_least_one_weekday()
    {
        var request = new GenerateSlotsRequest { From = Today, To = Today, Days = [] };

        Assert.Contains(_validator.Validate(request).Errors, e => e.PropertyName == nameof(request.Days));
    }
}
