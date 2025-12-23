using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace JwtTokenMiddleware;

public class JwtTokenGenerator
{
    private readonly JwtTokenRotationOptions _options;

    public JwtTokenGenerator(JwtTokenRotationOptions options)
    {
        _options = options;
    }

    public string GenerateToken(string userId, IEnumerable<Claim>? additionalClaims = null)
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
        var redis = ConnectionMultiplexer.Connect(_options.RedisConnectionString);
        redis.GetDatabase().StringSet(
            token.Id, 
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(), 
            TimeSpan.FromMinutes(_options.InactivityThreshold)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
