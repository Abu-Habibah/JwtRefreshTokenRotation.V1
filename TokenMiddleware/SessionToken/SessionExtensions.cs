using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace TokenMiddleware.SessionToken;

public static class SessionExtensions
{
    /// <summary>
    /// Registers the session service implementation with the dependency injection container.
    /// </summary>
    /// <remarks>This method adds a singleton instance of ISessionService using RedisSessionService as the
    /// implementation. It is intended for use in configuring session management in applications.</remarks>
    /// <param name="services">The collection of services to which the session service will be added.</param>
    /// <returns>The updated IServiceCollection instance to allow for method chaining.</returns>
    public static IServiceCollection AddSessionTokens(this IServiceCollection services)
    {
        services.AddSingleton<ISessionService, RedisSessionService>();
        return services;
    }

    /// <summary>
    /// Adds session-based authentication to the application's request pipeline by registering the
    /// SessionAuthenticationMiddleware.
    /// </summary>
    /// <remarks>Call this method after configuring session state in the application's middleware pipeline to
    /// ensure session authentication functions correctly.</remarks>
    /// <param name="app">The application builder used to configure the middleware pipeline. Cannot be null.</param>
    /// <returns>The same IApplicationBuilder instance, enabling method chaining.</returns>
    public static IApplicationBuilder UseSessionAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionAuthenticationMiddleware>();
    }
}
