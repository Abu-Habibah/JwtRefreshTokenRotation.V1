namespace JwtTokenMiddleware;

public class JwtTokenRotationOptions
{
    /// <summary>
    /// Gets or sets the duration in minutes of inactivity that must elapse before a user is considered inactive.
    /// </summary>
    /// <remarks>
    /// This threshold is used to determine when to expire authentication tokens due to inactivity.
    /// If a user does not make any requests within this time frame, 
    /// their token will be considered expired and they will need to re-authenticate.
    /// </remarks>
    public int InactivityThreshold { get; set; } = 10;

    /// <summary>
    /// Gets or sets the duration in minutes for which an authentication token remains valid before expiring.
    /// </summary>
    /// <remarks> 
    /// Token expiration is based on absolute time since issuance, regardless of activity.
    /// This should be set longer than the inactivity threshold to allow for token refreshes.
    /// </remarks>
    public int TokenExpiration { get; set; } = 120;

    /// <summary>
    /// Gets or sets the connection string used to connect to the Redis server.
    /// </summary>
    public string RedisConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Gets or sets the secret key used to sign and validate JSON Web Tokens (JWTs).
    /// </summary>
    /// <remarks>The secret should be a sufficiently long and random string to ensure the security of
    /// generated tokens. Changing this value will invalidate all previously issued tokens that were signed with the old
    /// secret.</remarks>
    public string JwtSecret { get; set; } = "your-secret-key";

    /// <summary>
    /// Gets or sets the issuer identifier for the application.
    /// </summary>
    public string Issuer { get; set; } = "JwtRefreshTokenRotation";

    /// <summary>
    /// Gets or sets the intended audience for the authentication token.
    /// </summary>
    public string Audience { get; set; }= "JwtRefreshTokenRotation-Users";
}
