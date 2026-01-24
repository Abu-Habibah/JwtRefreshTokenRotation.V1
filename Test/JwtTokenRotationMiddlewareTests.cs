using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using TokenMiddleware.JwtToken;
using Xunit.Abstractions;

public class JwtTokenRotationMiddlewareTests
{
    private readonly ITestOutputHelper _output;
    private readonly JwtTokenRotationOptions _options = new()
    {
        JwtSecret = "u1X9zPqQe7vNf4sTj8wYk2rLm5aB0cVdGhJxZpQnR3sUoWmYt",
        RedisConnectionString = "localhost:6379",
        InactivityThreshold = 1 // 1 minute for testing
    };

    public JwtTokenRotationMiddlewareTests(ITestOutputHelper output)
    {
        _output = output;
    }

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
    public async Task ExpiredByInactivity_ShouldReturn401_Fast()
    {
        var options = new JwtTokenRotationOptions
        {
            JwtSecret = _options.JwtSecret,
            RedisConnectionString = _options.RedisConnectionString,
            InactivityThreshold = 1, // still required for span property
            TokenExpiration = 2
        };

        // Force a very short threshold for testing
        //var threshold = TimeSpan.FromSeconds(1);

        var redis = ConnectionMultiplexer.Connect(options.RedisConnectionString);
        var generator = new JwtTokenGenerator(options, redis);
        var jwtToken = await generator.GenerateTokenAsync("user123");

        var middleware = new JwtTokenRotationMiddleware(
            async (ctx) => ctx.Response.StatusCode = 200,
            redis,
            options
        );

        // First request initializes last access
        var context1 = new DefaultHttpContext();
        context1.Request.Headers["Authorization"] = $"Bearer {jwtToken}";
        await middleware.InvokeAsync(context1);
        Assert.Equal(200, context1.Response.StatusCode);

        // Wait beyond threshold
        await Task.Delay(TimeSpan.FromSeconds(61));//more than 1 minute, the inactivity threshold

        // Second request should fail
        var context2 = new DefaultHttpContext();
        context2.Request.Headers["Authorization"] = $"Bearer {jwtToken}";
        await middleware.InvokeAsync(context2);
        Assert.Equal(401, context2.Response.StatusCode);
    }




    [Fact]
    public async Task AutoReissue_ShouldReturnNewTokenHeader_WhenTokenNearExpiry()
    {
        // Arrange: configure options with short expiration
        var options = new JwtTokenRotationOptions
        {
            JwtSecret = _options.JwtSecret,
            RedisConnectionString = _options.RedisConnectionString,
            InactivityThreshold = 2, // 1 minute
            TokenExpiration = 3,     // 2 minute absolute expiration
            TokenExpirationAutoExtend = true
        };

        var redis = ConnectionMultiplexer.Connect(options.RedisConnectionString);
        var generator = new JwtTokenGenerator(options, redis);

        // Generate a token with short lifetime (1 minute)
        var jwtToken = await generator.GenerateTokenAsync("user123");

        var middleware = new JwtTokenRotationMiddleware(
            async (ctx) => ctx.Response.StatusCode = 200,
            redis,
            options
        );

        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = $"Bearer {jwtToken}";

        // first request so it sets last access time
        await Task.Delay(TimeSpan.FromSeconds(60));

        // Act: invoke middleware
        await middleware.InvokeAsync(context);

        var context2 = new DefaultHttpContext();
        context2.Request.Headers["Authorization"] = $"Bearer {jwtToken}";

        // second request, delay request to be near expiry but within inactivity threshold
        await Task.Delay(TimeSpan.FromSeconds(70));

        // Act: invoke middleware
        await middleware.InvokeAsync(context2);

        // Assert: request passes
        Assert.Equal(200, context2.Response.StatusCode);

        // Assert: new token header is present
        Assert.True(context2.Response.Headers.ContainsKey("X-New-Token"),
            "Expected middleware to issue a new token header");

        var newToken = context.Response.Headers["X-New-Token"].ToString();


        Assert.False(string.IsNullOrEmpty(newToken), "New token should not be empty");

        // Verify new token is valid and different from old one
        var handler = new JwtSecurityTokenHandler();
        var oldJwt = handler.ReadJwtToken(jwtToken);
        var newJwt = handler.ReadJwtToken(newToken);

        _output.WriteLine($"Old Token: {oldJwt}");
        _output.WriteLine($"New Token: {newJwt}");
        
        Assert.NotEqual(oldJwt.Id, newJwt.Id);
        Assert.True(newJwt.ValidTo >= oldJwt.ValidTo,
            $"Expected new token expiry {newJwt.ValidTo} to be later than old {oldJwt.ValidTo}");

        // verify that the old token is purged from Redis
        var db = redis.GetDatabase();
        var oldTokenKey = $"jwt:lastaccess:{oldJwt.Id}";
        var oldTokenExists = await db.KeyExistsAsync(oldTokenKey);

        _output.WriteLine($"Old Token Key Exists in Redis: {oldTokenExists}");
        Assert.False(oldTokenExists, "Old token should be purged from Redis");
    }



}
