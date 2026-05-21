using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hangfire.Dashboard;
using Microsoft.IdentityModel.Tokens;

namespace BolaoCopa.Api;

public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;

    public HangfireAuthFilter(string secret, string issuer, string audience)
    {
        _secret = secret;
        _issuer = issuer;
        _audience = audience;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var token = ExtractToken(httpContext);
        if (token is null) return false;

        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = key,
            }, out _);

            return principal.HasClaim(ClaimTypes.Role, "Admin");
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractToken(HttpContext ctx)
    {
        var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (auth?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            return auth["Bearer ".Length..].Trim();
        return ctx.Request.Query["access_token"];
    }
}
