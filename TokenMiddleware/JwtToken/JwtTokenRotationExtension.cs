using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace TokenMiddleware.JwtToken;

public static class JwtTokenRotationExtension
{
    public static IServiceCollection AddJwtTokenRotation(this IServiceCollection services, JwtTokenRotationOptions options)
    {
        services.AddSingleton(options);

        // Ensure Redis is registered
        if (!services.Any(sd => sd.ServiceType == typeof(IConnectionMultiplexer)))
        {
            throw new InvalidOperationException(
                "Redis must be registered. Call AddRedisServer() before AddJwtTokenRotation().");
        }

        return services;
    }

    public static IApplicationBuilder UseJwtTokenRotation(this IApplicationBuilder app)
    {
        // Double-check at runtime
        var redis = app.ApplicationServices.GetService<IConnectionMultiplexer>();
        if (redis == null)
        {
            throw new InvalidOperationException(
                "Redis must be registered. Call AddRedisServer() before UseJwtTokenRotation().");
        }

        return app.UseMiddleware<JwtTokenRotationMiddleware>();
    }
}

