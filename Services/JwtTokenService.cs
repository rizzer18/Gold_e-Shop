using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Gold_e_Shop.Services;

public sealed class JwtSettings
{
    public string Secret { get; set; } = null!;
    public string Issuer { get; set; } = "Gold_eShop";
    public string Audience { get; set; } = "Gold_eShop";
    public int ExpiresHours { get; set; } = 12;
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _cfg;
    private readonly byte[] _key;

    public JwtTokenService(IOptions<JwtSettings> cfg)
    {
        _cfg = cfg.Value;
        _key = Encoding.UTF8.GetBytes(_cfg.Secret);
    }

    public (string token, DateTime expiresUtc) CreateToken(int userId, string role, string? username = null)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddHours(_cfg.ExpiresHours);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        };
        if (!string.IsNullOrWhiteSpace(username))
            claims.Add(new Claim(ClaimTypes.Name, username!));

        var creds = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _cfg.Issuer,
            audience: _cfg.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,

            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return (jwt, expires);
    }
}