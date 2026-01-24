using Microsoft.AspNetCore.Http;
using StackExchange.Redis;

namespace TokenMiddleware.RateLimiter;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redis;
    private readonly LimitingOptions _options;
    private readonly LoadedLuaScript _loadedScript;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IConnectionMultiplexer redis,
        LimitingOptions options,
        LoadedLuaScript loadedScript)
    {
        _next = next;
        _redis = redis.GetDatabase();
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loadedScript = loadedScript ?? throw new ArgumentNullException(nameof(loadedScript));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            bool isAuthorized = context.User?.Identity?.IsAuthenticated ?? false;
            string path = context.Request.Path.Value ?? string.Empty;

            var limitingOption = LimitingPolicyResolver.Resolve(_options, isAuthorized, path);

            var identity = isAuthorized ? context.User.Identity?.Name ?? "anonymous"
                                        : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var key = $"ratelimit:{identity}:{path}";

            // Execute preloaded Lua script atomically
            var raw = await _redis.ScriptEvaluateAsync(
                _loadedScript,
                new
                {
                    key = (RedisKey)key,
                    max = limitingOption.MaxRequests,
                    window = (int)limitingOption.WindowSpan.TotalSeconds
                }
            );

            var result = (RedisResult[])raw;
            var allowed = (int)result[0];
            var remaining = (int)result[1];
            var reset = (int)result[2];

            // Add standard rate limit headers
            context.Response.Headers[GeneralConst.X_LIMIT_LIMIT] = limitingOption.MaxRequests.ToString();
            context.Response.Headers[GeneralConst.X_LIMIT_REMAINING] = remaining.ToString();
            context.Response.Headers[GeneralConst.X_LIMIT_RESET] = reset.ToString();

            if (allowed == 0)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("Rate limit exceeded");
                return;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            // Fallback strategy can be configured via LimitingOptions.FallbackMode
            switch (_options.FallbackMode)
            {
                case RateLimitFallbackMode.FailOpen:
                    await _next(context);
                    break;
                case RateLimitFallbackMode.FailClosed:
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsync("Rate limiting unavailable, requests blocked");
                    break;
                case RateLimitFallbackMode.FailFast:
                default:
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsync($"Rate limiting failed: {ex.Message}");
                    break;
            }
        }
    }
}
