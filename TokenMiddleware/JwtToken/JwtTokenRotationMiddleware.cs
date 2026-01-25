using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;

namespace TokenMiddleware.JwtToken;

public class JwtTokenRotationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer _redisMux;
    private readonly IDatabase _redis;
    private readonly JwtTokenRotationOptions _options;

    public JwtTokenRotationMiddleware(RequestDelegate next, IConnectionMultiplexer redis, JwtTokenRotationOptions options)
    {
        _next = next;
        _redisMux = redis;
        _redis = redis.GetDatabase();
        _options = options;
    }

    /// <summary>
    /// Processes an HTTP request to validate a JWT bearer token, manage token expiration due to inactivity, and
    /// optionally issue a new token if auto-extension is enabled.
    /// </summary>
    /// <remarks>This method checks for a valid JWT bearer token in the Authorization header, validates the
    /// token's signature and claims, and enforces inactivity expiration using a Redis-backed store. If the token is
    /// nearing expiration and auto-extension is enabled, a new token is issued and returned in the response header. The
    /// method responds with appropriate HTTP status codes and messages for missing, invalid, or expired
    /// tokens.</remarks>
    /// <param name="context">The HTTP context for the current request, containing request and response information, including the
    /// Authorization header with the JWT bearer token.</param>
    /// <returns>A task that represents the asynchronous operation of token validation and processing.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing token");
            return;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Validate signature & claims
            var validationParameters = _options.TokenValidationParameters;
            handler.ValidateToken(token, validationParameters, out _);

            var jti = jwtToken.Id;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var lastAccessStr = await _redis.StringGetAsync(jti);
            if (!lastAccessStr.IsNullOrEmpty)
            {
                var lastAccess = long.Parse(lastAccessStr);
                if (now - lastAccess > _options.InactivityThresholdSpan.TotalMilliseconds)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Token expired due to inactivity");
                    return;
                }
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid token");
                return;
            }

            // Calculate remaining lifetime
            var remainingLifetime = jwtToken.ValidTo - DateTime.UtcNow;
            TimeSpan ttl = remainingLifetime;

            // Auto‑extend logic
            if (_options.TokenExpirationAutoExtend &&
                remainingLifetime < _options.InactivityThresholdSpan)
            {
                // Issue a new JWT with full expiration
                var generator = new JwtTokenGenerator(_options, _redisMux);
                var newToken = await generator.GenerateTokenAsync(jwtToken.Subject);

                var newJwt = handler.ReadJwtToken(newToken);
                var newJti = newJwt.Id;

                // Store new jti in Redis with full expiration span
                await _redis.StringSetAsync(newJti, now, _options.TokenExpirationSpan);

                // Invalidate old jti immediately
                await _redis.KeyDeleteAsync(jti);

                // Return new token to client via response header
                context.Response.Headers[GeneralConst.X_NEW_TOKEN] = newToken;

                ttl = _options.TokenExpirationSpan;
            }


            // Update Redis for current jti if not reissued
            await _redis.StringSetAsync(jti, now, ttl);

            await _next(context);
        }
        catch (Exception)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid token");
        }
    }
}
