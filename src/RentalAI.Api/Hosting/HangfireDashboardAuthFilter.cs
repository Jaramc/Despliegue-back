using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;

namespace RentalAI.Api.Hosting;

public sealed class HangfireDashboardAuthFilter(string user, string password) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        string? header = httpContext.Request.Headers.Authorization;

        if (header is not null && header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
            var separator = decoded.IndexOf(':');
            if (separator > 0
                && Matches(decoded[..separator], user)
                && Matches(decoded[(separator + 1)..], password))
            {
                return true;
            }
        }

        httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire\"";
        return false;
    }

    private static bool Matches(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
}
