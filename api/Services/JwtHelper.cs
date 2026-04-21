using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TheLibrary.Api.Services;

public class JwtHelper
{
    private readonly SymmetricSecurityKey _key;
    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtHelper(string signingKey)
    {
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }

    public string GenerateToken(string username, bool isAdmin = false)
    {
        var credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("isAdmin", isAdmin.ToString().ToLowerInvariant())
        };

        var token = new JwtSecurityToken(
            issuer: "the-library",
            audience: "the-library",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials
        );

        return _handler.WriteToken(token);
    }

    public string? ValidateToken(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "the-library",
            ValidateAudience = true,
            ValidAudience = "the-library",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _key,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        try
        {
            var principal = _handler.ValidateToken(token, parameters, out _);
            return principal.FindFirst(ClaimTypes.Name)?.Value;
        }
        catch
        {
            return null;
        }
    }

    public (string? Username, bool IsAdmin) ValidateTokenWithClaims(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "the-library",
            ValidateAudience = true,
            ValidAudience = "the-library",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _key,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        try
        {
            var principal = _handler.ValidateToken(token, parameters, out _);
            var username = principal.FindFirst(ClaimTypes.Name)?.Value;
            var isAdmin = principal.FindFirst("isAdmin")?.Value == "true";
            return (username, isAdmin);
        }
        catch
        {
            return (null, false);
        }
    }
}
