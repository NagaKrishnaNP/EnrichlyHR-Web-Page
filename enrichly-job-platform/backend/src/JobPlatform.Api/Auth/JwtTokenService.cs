using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JobPlatform.Api.Models.Entities;
using Microsoft.IdentityModel.Tokens;

namespace JobPlatform.Api.Auth;

public class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "enrichly-job-platform";
    public string Audience { get; set; } = "enrichly-job-platform";
    public int ExpiryMinutes { get; set; } = 120;
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(ApplicationUser user);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(ApplicationUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim("displayName", user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
