using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClientScout.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ClientScout.Application.Auth;

public class JwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(User user)
    {
        var secret = _config["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentNullException("Jwt:Secret missing");
        }

        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("Jwt:Secret must be at least 32 bytes.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim("FirstName", user.FirstName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
