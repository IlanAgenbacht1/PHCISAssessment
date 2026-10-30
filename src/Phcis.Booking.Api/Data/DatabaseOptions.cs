namespace Phcis.Booking.Api.Data;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>Convenient for local runs and the demo container. A real deployment runs migrations as a pipeline step.</summary>
    public bool MigrateOnStartup { get; init; }

    public bool SeedDemoData { get; init; }
}
