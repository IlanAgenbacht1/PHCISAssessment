using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Phcis.Booking.Api.Data;
using Phcis.Booking.Api.Features.Account;
using Phcis.Booking.Api.Features.Appointments;
using Phcis.Booking.Api.Features.Clinics;
using Phcis.Booking.Api.Features.Notifications;
using Phcis.Booking.Api.Features.Staff;
using Phcis.Booking.Api.Infrastructure;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Validation;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, config) => config
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Data
builder.Services.AddDbContext<BookingDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Booking"))
    .UseSnakeCaseNamingConvention());
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.Section));

// Auth: Identity with an HttpOnly cookie. The client is served from this same origin.
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<BookingDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "phcis.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;

    // An API answers with status codes, not redirects to a login page.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddBookingPolicies();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var permitsPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);
    options.AddPolicy(Policies.AuthRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

// Application
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidatorsFromAssemblyContaining<BookAppointmentRequestValidator>();
builder.Services.AddScoped<IClinicQueries, ClinicQueries>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IClinicScheduleService, ClinicScheduleService>();
builder.Services.AddScoped<INotificationService, LoggingNotificationService>();
builder.Services.AddScoped<DemoDataSeeder>();

// HTTP plumbing
builder.Services.ConfigureHttpJsonOptions(options => SharedJson.Configure(options.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BookingExceptionHandler>();
builder.Services.AddHealthChecks().AddDbContextCheck<BookingDbContext>();
builder.Services.AddOpenApi();

var app = builder.Build();

await PrepareDatabaseAsync(app);

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseBlazorFrameworkFiles();
app.MapStaticAssets();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAccountEndpoints();
app.MapClinicEndpoints();
app.MapAppointmentEndpoints();
app.MapStaffEndpoints();
app.MapHealthChecks("/health");

// Unknown /api routes should 404, not get the Blazor app's index.html.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

static async Task PrepareDatabaseAsync(WebApplication app)
{
    var options = app.Configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();
    if (!options.MigrateOnStartup && !options.SeedDemoData) return;

    await using var scope = app.Services.CreateAsyncScope();
    if (options.MigrateOnStartup)
        await scope.ServiceProvider.GetRequiredService<BookingDbContext>().Database.MigrateAsync();
    if (options.SeedDemoData)
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
}

public partial class Program;
