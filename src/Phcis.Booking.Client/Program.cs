using FluentValidation;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using Phcis.Booking.Client;
using Phcis.Booking.Client.Auth;
using Phcis.Booking.Client.Services;
using Phcis.Booking.Shared.Contracts;
using Phcis.Booking.Shared.Validation;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same origin as the API, so the browser attaches the auth cookie to these requests by itself.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddMudServices(options =>
{
    options.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
    options.SnackbarConfiguration.VisibleStateDuration = 6000;
});

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CookieAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CookieAuthenticationStateProvider>());

builder.Services.AddScoped<IAccountApi, AccountApi>();
builder.Services.AddScoped<IBookingApi, BookingApi>();
builder.Services.AddScoped<IStaffApi, StaffApi>();

// Registered one by one rather than by assembly scanning: scanning relies on reflection, which
// doesn't mix well with WASM trimming.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddTransient<IValidator<RegisterPatientRequest>, RegisterPatientRequestValidator>();
builder.Services.AddTransient<IValidator<BookAppointmentRequest>, BookAppointmentRequestValidator>();
builder.Services.AddTransient<IValidator<CancelAppointmentRequest>, CancelAppointmentRequestValidator>();
builder.Services.AddTransient<IValidator<GenerateSlotsRequest>, GenerateSlotsRequestValidator>();

await builder.Build().RunAsync();
