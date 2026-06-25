using System.Security.Claims;

namespace RentalAI.Common.Web;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue("sub");
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }
}
