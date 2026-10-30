using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MudBlazor.Services;

namespace Phcis.Booking.UnitTests.Client;

/// <summary>bUnit context with MudBlazor's services, and a fixed clock (Fri 30 Oct 2026, 09:00 SAST).</summary>
public abstract class MudTestContext : BunitContext
{
    protected static readonly TimeSpan Sast = TimeSpan.FromHours(2);

    protected MudTestContext()
    {
        // MudBlazor calls into JS for things like focus and popovers; none of it matters for these tests.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 30, 7, 0, 0, TimeSpan.Zero)));
    }

    protected static Phcis.Booking.Shared.Contracts.TimeSlotDto Slot(int id, int hour, int minute = 0) =>
        new(id, new DateTimeOffset(2026, 11, 2, hour, minute, 0, Sast), new DateTimeOffset(2026, 11, 2, hour, minute + 20, 0, Sast));
}
