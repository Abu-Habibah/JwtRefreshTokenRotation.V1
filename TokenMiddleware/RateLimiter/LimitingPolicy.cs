namespace TokenMiddleware.RateLimiter;

public class LimitingPolicy
{
    public LimitingPolicy()
    {
        GeneralOption = new GeneralLimitingOption();
        EndpointOptions = new List<EndpointLimitingOption>();
    }

    /// <summary>
    /// The global/general option (always present).
    /// </summary>
    public GeneralLimitingOption GeneralOption { get; set; }

    /// <summary>
    /// Endpoint-specific overrides (optional).
    /// </summary>
    public List<EndpointLimitingOption> EndpointOptions { get; set; }
}
