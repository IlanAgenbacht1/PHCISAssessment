namespace Phcis.Booking.Shared;

/// <summary>Route templates shared by the API and the client so they can't drift apart.</summary>
public static class ApiRoutes
{
    public const string Account = "api/account";
    public const string Clinics = "api/clinics";
    public const string Appointments = "api/appointments";
    public const string Staff = "api/staff";

    public static string ClinicSlots(int clinicId, DateOnly date) => $"{Clinics}/{clinicId}/slots?date={date:yyyy-MM-dd}";
    public static string Appointment(string reference) => $"{Appointments}/{Uri.EscapeDataString(reference)}";
    public static string CancelAppointment(string reference) => $"{Appointment(reference)}/cancel";
    public static string RescheduleAppointment(string reference) => $"{Appointment(reference)}/reschedule";
    public static string StaffDayList(DateOnly date) => $"{Staff}/day-list?date={date:yyyy-MM-dd}";
    public static string StaffAttendance(int appointmentId) => $"{Staff}/appointments/{appointmentId}/attendance";
    public static string StaffSlots => $"{Staff}/slots";
}
