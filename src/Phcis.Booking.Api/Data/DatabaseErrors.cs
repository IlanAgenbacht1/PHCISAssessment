using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Phcis.Booking.Api.Data;

internal static class DatabaseErrors
{
    public static bool IsUniqueViolation(this DbUpdateException ex, string indexName) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName == indexName;
}
