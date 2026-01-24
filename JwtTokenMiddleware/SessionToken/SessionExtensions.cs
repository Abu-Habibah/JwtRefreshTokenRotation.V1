using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace TokenMiddleware.SessionToken;

public static class SessionExtensions
{
    public static IServiceCollection AddSessionTokens(this IServiceCollection services)
    {
        services.AddSingleton<ISessionService, RedisSessionService>();
        return services;
    }

    public static IApplicationBuilder UseSessionAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionAuthenticationMiddleware>();
    }
}
