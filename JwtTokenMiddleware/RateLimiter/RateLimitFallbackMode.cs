namespace JwtTokenMiddleware.RateLimiter;

public enum RateLimitFallbackMode
{
    /// <summary>
    /// allow all requests if Redis fails
    /// </summary>
    FailOpen, 

    /// <summary>
    /// block all requests if Redis fails
    /// </summary>
    FailClosed, 

    /// <summary>
    /// return 500 if Redis fails
    /// </summary>
    FailFast
}
