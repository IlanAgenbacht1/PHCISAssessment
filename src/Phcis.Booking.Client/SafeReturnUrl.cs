namespace Phcis.Booking.Client;

public static class SafeReturnUrl
{
    /// <summary>
    /// Only follows app-relative return URLs. Without this, a link like
    /// login?returnUrl=https://evil.example would bounce a freshly signed-in user off-site.
    /// </summary>
    public static string Resolve(string? returnUrl, string fallback)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return fallback;
        if (returnUrl.StartsWith("//") || returnUrl.StartsWith("/\\") || returnUrl.Contains(':')) return fallback;
        return Uri.TryCreate(returnUrl, UriKind.Relative, out _) ? returnUrl.TrimStart('/') : fallback;
    }
}
