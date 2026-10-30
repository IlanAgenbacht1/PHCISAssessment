using Bunit;
using Phcis.Booking.Client.Components;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.UnitTests.Client;

public class SlotPickerTests : MudTestContext
{
    [Fact]
    public void Shows_clinic_time_split_into_morning_and_afternoon()
    {
        // The test machine runs in UTC. 09:00 and 14:20 here are SAST and must render as such.
        var cut = Render<SlotPicker>(p => p.Add(x => x.Slots, [Slot(1, 9), Slot(2, 14, 20)]));

        var groups = cut.FindAll("[role=radiogroup]");
        Assert.Equal("Morning times", groups[0].GetAttribute("aria-label"));
        Assert.Equal("Afternoon times", groups[1].GetAttribute("aria-label"));
        Assert.Equal(["09:00", "14:20"], cut.FindAll("[data-slot-id]").Select(b => b.TextContent.Trim()));
    }

    [Fact]
    public void Clicking_a_slot_raises_SelectedSlotIdChanged()
    {
        int? selected = null;
        var cut = Render<SlotPicker>(p => p
            .Add(x => x.Slots, [Slot(1, 9), Slot(2, 10)])
            .Add(x => x.SelectedSlotIdChanged, id => selected = id));

        cut.Find("[data-slot-id='2']").Click();

        Assert.Equal(2, selected);
    }

    [Fact]
    public void Selected_slot_is_marked_for_screen_readers()
    {
        var cut = Render<SlotPicker>(p => p
            .Add(x => x.Slots, [Slot(1, 9), Slot(2, 10)])
            .Add(x => x.SelectedSlotId, 2));

        Assert.Equal("false", cut.Find("[data-slot-id='1']").GetAttribute("aria-checked"));
        Assert.Equal("true", cut.Find("[data-slot-id='2']").GetAttribute("aria-checked"));
    }

    [Fact]
    public void Empty_day_shows_message_instead_of_buttons()
    {
        var cut = Render<SlotPicker>(p => p.Add(x => x.Slots, Array.Empty<TimeSlotDto>()));

        Assert.Empty(cut.FindAll("[data-slot-id]"));
        Assert.Contains("No times left", cut.Markup);
    }
}
