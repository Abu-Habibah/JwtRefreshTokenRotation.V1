using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

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
            issuer: _options.Issuer,               // can be set via options
            audience: _options.Audience,       // can be set via options
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1), // absolute expiration
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
