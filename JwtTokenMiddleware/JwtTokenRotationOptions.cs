using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace JwtTokenMiddleware;

public class JwtTokenRotationOptions
{

    /// <summary>
    /// Auto -extend token expiration on activity.
    /// when remaining token life time is less than InactivityThreshold.
    /// </summary>
    public bool TokenExpirationAutoExtend { get; set; } = true;

    /// <summary>
    /// Duration in minutes of inactivity before a token is considered expired.
    /// </summary>
    public int InactivityThreshold { get; set; } = 10;
    public TimeSpan InactivityThresholdSpan => TimeSpan.FromMinutes(InactivityThreshold);

    /// <summary>
    /// Absolute expiration in minutes for a token.
    /// </summary>
    public int TokenExpiration { get; set; } = 120;
    public TimeSpan TokenExpirationSpan => TimeSpan.FromMinutes(TokenExpiration);

    /// <summary>
    /// Redis connection string.
    /// </summary>
    public string RedisConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Secret key for signing JWTs. Must be >= 32 characters (256 bits).
    /// </summary>
    public string JwtSecret { get; set; } = "u1X9zPqQe7vNf4sTj8wYk2rLm5aB0cVdGhJxZpQnR3sUoWmYt";

    /// <summary>
    /// Issuer identifier.
    /// </summary>
    public string Issuer { get; set; } = "JwtRefreshTokenRotation";

    /// <summary>
    /// Audience identifier.
    /// </summary>
    public string Audience { get; set; } = "JwtRefreshTokenRotation-Users";

    private TokenValidationParameters? _tokenValidationParameters;

    /// <summary>
    /// Gets or sets the parameters used to validate JSON Web Tokens (JWTs) during authentication.
    /// Allow user to override the default validation parameters.
    /// </summary>
    public TokenValidationParameters TokenValidationParameters
    {
        get
        {
            if (_tokenValidationParameters != null) return _tokenValidationParameters;

            // Default if user hasn't supplied one
            return new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        }
        set => _tokenValidationParameters = value;
    }
}
