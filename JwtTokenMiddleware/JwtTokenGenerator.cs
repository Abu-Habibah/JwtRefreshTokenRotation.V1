using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace JwtTokenMiddleware;

public class JwtTokenGenerator
{
    private readonly JwtTokenRotationOptions _options;
    private readonly IDatabase _redis;

    public JwtTokenGenerator(JwtTokenRotationOptions options, IConnectionMultiplexer redis)
    {
        _options = options;
        _redis = redis.GetDatabase();
    }

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

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.TokenExpiration),
            signingCredentials: credentials
        );

        //TODO: add token jti to Redis with initial last access time?
        bool set = await _redis.StringSetAsync(token.Id,
                                               DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                               _options.TokenExpirationSpan);

        if (!set)
        {
            throw new Exception("Failed to store token in Redis");
        }

        return new JwtSecurityTokenHandler().WriteToken(token);

    }
}
