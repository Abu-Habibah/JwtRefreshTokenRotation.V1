using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace TokenMiddleware.JwtToken;

public static class JwtTokenRotationExtension
{
    /// <summary>
    /// Registers JWT token rotation services with the specified options in the dependency injection container.
    /// </summary>
    /// <remarks>This method requires that Redis is registered in the service collection prior to adding JWT
    /// token rotation services. Ensure that AddRedisServer() is called before this method to avoid runtime
    /// exceptions.</remarks>
    /// <param name="services">The service collection to which the JWT token rotation services will be added.</param>
    /// <param name="options">The options that configure the behavior of JWT token rotation.</param>
    /// <returns>The updated service collection with JWT token rotation services registered.</returns>
    /// <exception cref="InvalidOperationException">Thrown if Redis is not registered in the service collection. Call AddRedisServer() before invoking this method.</exception>
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

    /// <summary>
    /// Configures the application to use JWT token rotation middleware for enhanced security.
    /// </summary>
    /// <remarks>JWT token rotation helps prevent token reuse by rotating tokens on each authentication event.
    /// This middleware requires Redis to manage token state and must be registered prior to use.</remarks>
    /// <param name="app">The application builder to configure with JWT token rotation middleware.</param>
    /// <returns>The application builder instance, enabling further middleware configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown if Redis is not registered in the service container. Ensure that AddRedisServer() is called before
    /// invoking this method.</exception>
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

