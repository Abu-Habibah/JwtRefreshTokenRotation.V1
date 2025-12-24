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
        InactivityThreshold = 1 // 1 minute for testing
    };

    private async Task<(string jwtToken, ConnectionMultiplexer redis)> GenerateJwtAsync(string userId)
    {
        var redis = ConnectionMultiplexer.Connect(_options.RedisConnectionString);
        var generator = new JwtTokenGenerator(_options, redis);
        return (await generator.GenerateTokenAsync(userId), redis);
    }

    /// <summary>
    /// Helper to simulate a request with a given token.
    /// </summary>
    private async Task<int> InvokeWithTokenAsync(JwtTokenRotationMiddleware middleware, string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = $"Bearer {token}";
        await middleware.InvokeAsync(context);
        return context.Response.StatusCode;
    }

    [Fact]
    public async Task ValidToken_ShouldPassMiddleware()
    {
        var r = await GenerateJwtAsync("user123");
        var middleware = new JwtTokenRotationMiddleware(
            async (ctx) => ctx.Response.StatusCode = 200,
            r.redis,
            _options
        );

        var statusCode = await InvokeWithTokenAsync(middleware, r.jwtToken);
        Assert.Equal(200, statusCode);
    }

    [Fact]
    public async Task ExpiredByInactivity_ShouldReturn401()
    {
        var r = await GenerateJwtAsync("user123");
        var middleware = new JwtTokenRotationMiddleware(
            async (ctx) => ctx.Response.StatusCode = 200,
            r.redis,
            _options
        );

        // First request initializes last access
        var statusCode1 = await InvokeWithTokenAsync(middleware, r.jwtToken);
        Assert.Equal(200, statusCode1);

        // Wait beyond inactivity threshold (slightly over 1 minute)
        await Task.Delay(TimeSpan.FromMinutes(1.1));

        // Second request should fail
        var statusCode2 = await InvokeWithTokenAsync(middleware, r.jwtToken);
        Assert.Equal(401, statusCode2);
    }
}
