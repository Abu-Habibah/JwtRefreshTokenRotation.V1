using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace TokenMiddleware.SessionToken;

public class SessionAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionAuthenticationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ISessionService sessions)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader?.StartsWith(GeneralConst.SESSION_TOKEN_MARKER, StringComparison.OrdinalIgnoreCase) == true)
        {
            var token = authHeader.Substring(GeneralConst.SESSION_TOKEN_MARKER.Length).Trim();
            var payload = await sessions.ValidateSessionAsync(token, TimeSpan.FromMinutes(30));

            if (payload != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, payload.UserId),
                    new Claim(ClaimTypes.Role, payload.Role),
                    new Claim("policy", payload.Policy)
                };

                if (payload.ExtraClaims != null)
                {
                    claims.AddRange(payload.ExtraClaims.Select(kv => new Claim(kv.Key, kv.Value)));
                }

                var identity = new ClaimsIdentity(claims, GeneralConst.SESSION_TOKEN_MARKER);
                context.User = new ClaimsPrincipal(identity);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await _next(context);
    }
}
