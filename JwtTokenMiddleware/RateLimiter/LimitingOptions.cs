namespace JwtTokenMiddleware.RateLimiter;

public class LimitingOptions
{
    /// <summary>
    /// Limiting options for unauthorized users.
    /// If both GeneralOption and EndpointOptions are provided,
    /// when an endpoint is not found in EndpointOptions,
    /// GeneralOption will be applied.
    /// </summary>
    public LimitingPolicy UnauthorizedOptions { get; set; } = new();

    /// <summary>
    /// Limiting options for authorized users.
    /// If both GeneralOption and EndpointOptions are provided,
    /// when an endpoint is not found in EndpointOptions,
    /// GeneralOption will be applied.
    /// </summary>
    public LimitingPolicy AuthorizedOptions { get; set; } = new();

    /// <summary>
    /// Fallback mode when Redis is unavailable.
    /// </summary>
    public RateLimitFallbackMode FallbackMode { get; set; } = RateLimitFallbackMode.FailFast;
}
