using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace TokenMiddleware.RateLimiter
{
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

        // ---------------------------
        // Session Token Integration
        // ---------------------------

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

    // Session payload model
    public record SessionPayload(
        string UserId,
        string Role,
        string Policy,
        IDictionary<string, string>? ExtraClaims = null
    );

    public interface ISessionService
    {
        Task<string> CreateSessionAsync(SessionPayload payload, TimeSpan ttl);
        Task<SessionPayload?> ValidateSessionAsync(string token, TimeSpan? slidingTtl = null);
        Task InvalidateSessionAsync(string token);
    }

    public class RedisSessionService : ISessionService
    {
        private readonly IDatabase _db;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public RedisSessionService(IConnectionMultiplexer mux)
        {
            _db = mux.GetDatabase();
        }

        public async Task<string> CreateSessionAsync(SessionPayload payload, TimeSpan ttl)
        {
            var token = Guid.NewGuid().ToString("N");
            var key = $"session:{token}";
            var json = JsonSerializer.Serialize(payload, _jsonOptions);

            await _db.StringSetAsync(key, json, ttl);
            return token;
        }

        public async Task<SessionPayload?> ValidateSessionAsync(string token, TimeSpan? slidingTtl = null)
        {
            var key = $"session:{token}";
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return null;

            if (slidingTtl.HasValue)
                await _db.KeyExpireAsync(key, slidingTtl);

            // Explicitly convert RedisValue to string to avoid ambiguity
            return JsonSerializer.Deserialize<SessionPayload>(value.ToString(), _jsonOptions);
        }

        public Task InvalidateSessionAsync(string token)
            => _db.KeyDeleteAsync($"session:{token}");
    }

    public class SessionAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionAuthenticationMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, ISessionService sessions)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader?.StartsWith("Session ", StringComparison.OrdinalIgnoreCase) == true)
            {
                var token = authHeader.Substring("Session ".Length).Trim();
                var payload = await sessions.ValidateSessionAsync(token, TimeSpan.FromMinutes(30));

                if (payload != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, payload.UserId),
                        new Claim(ClaimTypes.Role, payload.Role),
                        new Claim("policy", payload.Policy)
                    };

                    if (payload.ExtraClaims != null)
                    {
                        claims.AddRange(payload.ExtraClaims.Select(kv => new Claim(kv.Key, kv.Value)));
                    }

                    var identity = new ClaimsIdentity(claims, "Session");
                    context.User = new ClaimsPrincipal(identity);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }

            await _next(context);
        }
    }
}
