using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace JwtTokenMiddleware.RateLimiter;

public static class RateLimitingExtension
{
    public static IServiceCollection AddRateLimiter(this IServiceCollection services, LimitingOptions options)
    {
        services.AddSingleton(options);

        // Ensure Redis is registered
        var redisDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IConnectionMultiplexer));
        if (redisDescriptor == null)
        {
            throw new InvalidOperationException(
                "Redis must be registered. Call AddRedisServer() before AddRateLimiter().");
        }

        // Build a temporary provider to resolve IConnectionMultiplexer
        using var sp = services.BuildServiceProvider();
        var redis = sp.GetRequiredService<IConnectionMultiplexer>();
        var server = redis.GetServer(redis.GetEndPoints().First());

        // Prepare and load Lua script once
        var script = LuaScript.Prepare(@"
            local current = redis.call('INCR', @key)
            if current == 1 then
                redis.call('EXPIRE', @key, @window)
            end
            local ttl = redis.call('TTL', @key)
            local remaining = tonumber(@max) - current
            if remaining < 0 then
                remaining = 0
            end
            if current > tonumber(@max) then
                return {0, remaining, ttl}
            else
                return {1, remaining, ttl}
            end
        ");

        var loadedScript = script.Load(server);

        // Register LoadedLuaScript for injection into middleware
        services.AddSingleton(loadedScript);

        return services;
    }

    public static IApplicationBuilder UseRateLimitingMiddleware(this IApplicationBuilder app)
    {
        var redis = app.ApplicationServices.GetService<IConnectionMultiplexer>();
        if (redis == null)
        {
            throw new InvalidOperationException(
                "Redis must be registered. Call AddRedisServer() before UseRateLimitingMiddleware().");
        }

        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}

