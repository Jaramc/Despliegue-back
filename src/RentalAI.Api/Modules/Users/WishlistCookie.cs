using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace RentalAI.Api.Modules.Users;

public sealed class WishlistCookie
{
    public const string CookieName = "wishlist";

    private readonly IDataProtector protector;

    public WishlistCookie(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector("RentalAI.Wishlist");
    }

    public IReadOnlyList<Guid> Read(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrEmpty(value))
        {
            return [];
        }

        try
        {
            var json = protector.Unprotect(value);
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Write(HttpResponse response, IEnumerable<Guid> propertyIds)
    {
        var json = JsonSerializer.Serialize(propertyIds.Distinct());
        response.Cookies.Append(CookieName, protector.Protect(json), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = false,
            IsEssential = true,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    public void Clear(HttpResponse response) => response.Cookies.Delete(CookieName);
}
