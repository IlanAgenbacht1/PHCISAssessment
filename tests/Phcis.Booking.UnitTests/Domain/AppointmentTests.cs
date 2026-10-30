using Phcis.Booking.Domain;
using Phcis.Booking.Domain.Appointments;
using static Phcis.Booking.UnitTests.Domain.TestSlots;

namespace Phcis.Booking.UnitTests.Domain;

public class AppointmentTests
{
    [Fact]
    public void Book_future_slot_creates_booked_appointment_with_reference()
    {
        var appointment = Appointment.Book(patientId: 7, InHours(2), "  Repeat prescription ", Now);

        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        Assert.Equal(7, appointment.PatientId);
        Assert.Equal("Repeat prescription", appointment.ReasonForVisit);
        Assert.Matches("^PHC-[A-Z2-9]{6}$", appointment.Reference);
    }

    [Fact]
    public void Book_blank_reason_is_stored_as_null()
    {
        var appointment = Appointment.Book(1, InHours(2), "   ", Now);

        Assert.Null(appointment.ReasonForVisit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Book_slot_that_has_started_is_rejected(double hoursFromNow)
    {
        Assert.Throws<BookingRuleException>(() => Appointment.Book(1, InHours(hoursFromNow), null, Now));
    }

    [Fact]
    public void Cancel_frees_the_slot_and_keeps_the_reason()
    {
        var appointment = Appointment.Book(1, InHours(2), null, Now);

        appointment.Cancel("Feeling better", Now.AddMinutes(5));

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.False(appointment.Status.HoldsSlot());
        Assert.Equal("Feeling better", appointment.CancellationReason);
        Assert.Equal(Now.AddMinutes(5), appointment.CancelledUtc);
    }

    [Fact]
    public void Cancel_twice_is_rejected()
    {
        var appointment = Appointment.Book(1, InHours(2), null, Now);
        appointment.Cancel("First", Now);

        Assert.Throws<BookingRuleException>(() => appointment.Cancel("Second", Now));
    }

    [Fact]
    public void Cancel_after_start_is_rejected()
    {
        var appointment = Appointment.Book(1, InHours(1), null, Now);

        Assert.Throws<BookingRuleException>(() => appointment.Cancel("Too late", Now.AddHours(1)));
    }

    [Fact]
    public void Reschedule_marks_original_and_links_replacement()
    {
        var original = Appointment.Book(1, InHours(2, id: 10), "Check-up", Now);
        var newSlot = InHours(26, id: 11);

        var replacement = original.RescheduleTo(newSlot, Now);

        Assert.Equal(AppointmentStatus.Rescheduled, original.Status);
        Assert.False(original.Status.HoldsSlot());
        Assert.Equal(AppointmentStatus.Booked, replacement.Status);
        Assert.Equal(11, replacement.TimeSlotId);
        Assert.Equal(original.Id, replacement.RescheduledFromId);
        Assert.Equal("Check-up", replacement.ReasonForVisit);
        Assert.Contains(replacement.Reference, original.CancellationReason);
    }

    [Fact]
    public void Reschedule_to_same_slot_is_rejected()
    {
        var original = Appointment.Book(1, InHours(2, id: 10), null, Now);

        Assert.Throws<BookingRuleException>(() => original.RescheduleTo(InHours(2, id: 10), Now));
    }

    [Fact]
    public void Reschedule_to_another_clinic_is_rejected()
    {
        var original = Appointment.Book(1, InHours(2, id: 10, clinicId: 1), null, Now);

        Assert.Throws<BookingRuleException>(() => original.RescheduleTo(InHours(3, id: 11, clinicId: 2), Now));
    }

    [Fact]
    public void Reschedule_to_past_slot_leaves_original_untouched()
    {
        var original = Appointment.Book(1, InHours(2, id: 10), null, Now);

        Assert.Throws<BookingRuleException>(() => original.RescheduleTo(InHours(-1, id: 11), Now));
        Assert.Equal(AppointmentStatus.Booked, original.Status);
    }

    [Theory]
    [InlineData(true, AppointmentStatus.Attended)]
    [InlineData(false, AppointmentStatus.NoShow)]
    public void Record_attendance_after_start(bool attended, AppointmentStatus expected)
    {
        var appointment = Appointment.Book(1, InHours(1), null, Now);

        appointment.RecordAttendance(attended, Now.AddHours(1).AddMinutes(5));

        Assert.Equal(expected, appointment.Status);
        Assert.True(appointment.Status.HoldsSlot());
    }

    [Fact]
    public void Record_attendance_before_start_is_rejected()
    {
        var appointment = Appointment.Book(1, InHours(1), null, Now);

        Assert.Throws<BookingRuleException>(() => appointment.RecordAttendance(true, Now));
    }

    [Fact]
    public void Record_attendance_on_cancelled_appointment_is_rejected()
    {
        var appointment = Appointment.Book(1, InHours(1), null, Now);
        appointment.Cancel("No longer needed", Now);

        Assert.Throws<BookingRuleException>(() => appointment.RecordAttendance(false, Now.AddHours(2)));
    }

    [Fact]
    public void CanBeChanged_only_while_booked_and_not_started()
    {
        var appointment = Appointment.Book(1, InHours(1), null, Now);

        Assert.True(appointment.CanBeChanged(Now));
        Assert.False(appointment.CanBeChanged(Now.AddHours(1)));
    }
}
