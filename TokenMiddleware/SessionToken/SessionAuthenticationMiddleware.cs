using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace TokenMiddleware.SessionToken;

public class SessionAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionAuthenticationMiddleware(RequestDelegate next) => _next = next;

    /// <summary>
    /// Processes an HTTP request by validating a session token and setting the user principal if the token is valid.
    /// </summary>
    /// <remarks>If a valid session token is present in the Authorization header, the user principal is set on
    /// the context. If the token is invalid or missing, the response status is set to 401 Unauthorized and the request
    /// is not further processed.</remarks>
    /// <param name="context">The HTTP context for the current request, providing access to request and response information.</param>
    /// <param name="sessions">An implementation of ISessionService used to validate the session token and retrieve user session data.</param>
    /// <returns>A task that represents the asynchronous operation of processing the HTTP request.</returns>
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
