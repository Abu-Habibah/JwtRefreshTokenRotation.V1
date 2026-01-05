namespace JwtTokenMiddleware.RateLimiter;

public interface ILimitingOption
{
    int MaxRequests { get; set; }
    TimeSpan WindowSpan { get; set; }
}

public class GeneralLimitingOption : ILimitingOption
{
    public int MaxRequests { get; set; } = 60;
    public TimeSpan WindowSpan { get; set; } = TimeSpan.FromMinutes(1);
}

public class EndpointLimitingOption : GeneralLimitingOption
{
    public string Endpoint { get; set; } = string.Empty;
}
