using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Xunit;
using JwtTokenMiddleware;

public class JwtTokenRotationMiddlewareTests
{
    private readonly JwtTokenRotationOptions _options = new()
    {
        JwtSecret = "u1X9zPqQe7vNf4sTj8wYk2rLm5aB0cVdGhJxZpQnR3sUoWmYt",
        RedisConnectionString = "localhost:6379",
        InactivityThreshold = 1 // short threshold for testing
    };

    private string GenerateJwt(string userId)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        JwtTokenGenerator generator = new JwtTokenGenerator(_options);
        return generator.GenerateToken(userId);

    }

    [Fact]
    public async Task ValidToken_ShouldPassMiddleware()
    {
        var redis = ConnectionMultiplexer.Connect(_options.RedisConnectionString);
        var middleware = new JwtTokenRotationMiddleware(
            async (ctx) => ctx.Response.StatusCode = 200,
            redis,
            _options
        );

        var context = new DefaultHttpContext();
        var token = GenerateJwt("user123");
        context.Request.Headers["Authorization"] = $"Bearer {token}";

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task ExpiredByInactivity_ShouldReturn401()
    {
        var redis = ConnectionMultiplexer.Connect(_options.RedisConnectionString);
        var middleware = new JwtTokenRotationMiddleware(
            async (ctx) => ctx.Response.StatusCode = 200,
            redis,
            _options
        );

        var context = new DefaultHttpContext();
        var token = GenerateJwt("user123");
        context.Request.Headers["Authorization"] = $"Bearer {token}";

        // First request initializes last access
        await middleware.InvokeAsync(context);

        // Wait beyond inactivity threshold
        await Task.Delay(6500);

        var context2 = new DefaultHttpContext();
        context2.Request.Headers["Authorization"] = $"Bearer {token}";

        await middleware.InvokeAsync(context2);

        Assert.Equal(401, context2.Response.StatusCode);
    }
}
