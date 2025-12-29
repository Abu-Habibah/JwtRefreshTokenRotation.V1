using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace JwtTokenMiddleware;

public class JwtTokenGeneratorX
{
    private readonly JwtTokenRotationOptions _options;
    private readonly IDatabase _redis;

    public JwtTokenGeneratorX(JwtTokenRotationOptions options, IConnectionMultiplexer redis)
    {
        _options = options;
        _redis = redis.GetDatabase();
    }

    // Production generator always uses TokenExpirationSpan
    public async Task<string> GenerateTokenAsync(string userId)
    {
        return await GenerateTokenInternalAsync(userId, _options.TokenExpirationSpan);
    }

    // Internal helper allows custom expiration (used in tests)
    internal async Task<string> GenerateTokenInternalAsync(string userId, TimeSpan expiresIn)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_options.JwtSecret);

        var jti = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        }),
            Expires = now.Add(expiresIn),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = handler.CreateToken(tokenDescriptor);
        var jwt = handler.WriteToken(token);

        var result = await _redis.StringGetWithExpiryAsync(jti);
        await _redis.StringSetAsync(jti, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), expiresIn);

        return jwt;
    }


    [Obsolete("Use GenerateTokenAsync(string userId) instead")]
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
