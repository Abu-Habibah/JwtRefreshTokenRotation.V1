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
    //private readonly TimeSpan _inactivityThreshold = TimeSpan.FromMinutes(15);
    private readonly JwtTokenRotationOptions _options;
    public JwtTokenRotationMiddleware(RequestDelegate next, IConnectionMultiplexer redis, JwtTokenRotationOptions options)
    {
        _next = next;
        _redis = redis.GetDatabase();
        _options = options;
        //_inactivityThreshold = TimeSpan.FromMinutes(_options.InactivityThreshold);
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

            //TODO: if jti is not found in redis, consider it invalid/expired
            // jti should be added to redis when token is generated in JwtTokenGenerator
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid token");
                return;
            }

            // Update last access + TTL = remaining token lifetime
            var remainingLifetime = jwtToken.ValidTo - DateTime.UtcNow; 
            await _redis.StringSetAsync(jti, now, remainingLifetime);

            await _next(context);
        }
        catch (Exception)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid token");
        }
    }
}