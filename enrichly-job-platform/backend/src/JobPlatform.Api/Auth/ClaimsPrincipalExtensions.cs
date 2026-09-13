using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace JobPlatform.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? throw new InvalidOperationException("No user id claim present on principal.");
        return Guid.Parse(raw);
    }
}
