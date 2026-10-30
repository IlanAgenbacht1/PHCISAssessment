using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Phcis.Booking.Domain;

namespace Phcis.Booking.Api.Infrastructure;

/// <summary>
/// Turns expected failures into ProblemDetails responses. Anything not listed here is a bug and
/// falls through to the default 500 handler, which logs it.
/// </summary>
internal sealed class BookingExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            SlotUnavailableException => (StatusCodes.Status409Conflict, "Slot unavailable"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Changed by someone else"),
            BookingRuleException => (StatusCodes.Status422UnprocessableEntity, "Booking rule"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            _ => (0, "")
        };

        if (status == 0) return false;

        var detail = exception is DbUpdateConcurrencyException
            ? "This appointment was updated by someone else. Refresh and try again."
            : exception.Message;

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
