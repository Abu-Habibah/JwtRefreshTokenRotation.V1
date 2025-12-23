using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text;

namespace JwtTokenMiddleware;

public class JwtTokenRotationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redis;
    private readonly TimeSpan _inactivityThreshold = TimeSpan.FromMinutes(15);
    private readonly JwtTokenRotationOptions _options;
    public JwtTokenRotationMiddleware(RequestDelegate next, IConnectionMultiplexer redis, JwtTokenRotationOptions options)
    {
        _next = next;
        _redis = redis.GetDatabase();
        _options = options;
        _inactivityThreshold = TimeSpan.FromMinutes(_options.InactivityThreshold);
    }

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
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true
            };

            handler.ValidateToken(token, validationParameters, out _);

            var jti = jwtToken.Id;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var lastAccessStr = await _redis.StringGetAsync(jti);
            if (!lastAccessStr.IsNullOrEmpty)
            {
                var lastAccess = long.Parse(lastAccessStr);
                if (now - lastAccess > _inactivityThreshold.TotalMilliseconds)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Token expired due to inactivity");
                    return;
                }
            }

            // Update last access and set TTL
            await _redis.StringSetAsync(jti, now.ToString(), _inactivityThreshold);

            await _next(context);
        }
        catch (Exception)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid token");
        }
    }
}