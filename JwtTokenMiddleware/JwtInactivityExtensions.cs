using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace JwtTokenMiddleware;

public static class JwtTokenRotationExtension
{
    public static IServiceCollection AddJwtTokenRotation(this IServiceCollection services, JwtTokenRotationOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options.RedisConnectionString));
        return services;
    }

    public static IApplicationBuilder UseJwtJwtTokenRotation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<JwtTokenRotationMiddleware>();
    }
}