using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace TokenMiddleware.JwtToken;

public class JwtTokenGenerator
{
    private readonly JwtTokenRotationOptions _options;
    private readonly IConnectionMultiplexer _redis;

    public JwtTokenGenerator(JwtTokenRotationOptions options, IConnectionMultiplexer redis)
    {
        _options = options;
        _redis = redis;
    }

    /// <summary>
    /// Generates a JWT using TokenExpirationSpan from options.
    /// Supports adding extra claims and stores the jti in Redis with TTL.
    /// </summary>
    public async Task<string> GenerateTokenAsync(string userId, IEnumerable<Claim>? additionalClaims = null)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (additionalClaims != null)
            claims.AddRange(additionalClaims);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.Add(_options.TokenExpirationSpan),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        var jwt = handler.WriteToken(token);

        var jti = claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var db = _redis.GetDatabase();
        bool set = await db.StringSetAsync(
            jti,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            _options.TokenExpirationSpan
        );

        if (!set)
        {
            throw new Exception("Failed to store token in Redis");
        }

        return jwt;
    }
}
