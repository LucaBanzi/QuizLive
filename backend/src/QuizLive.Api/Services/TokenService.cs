using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace QuizLive.Api.Services;

public sealed class TokenService(IConfiguration cfg)
{
    public const string RoleClaim = "role";
    public const string NameClaim = "name";

    public static SymmetricSecurityKey Key(string secret) => new(Encoding.UTF8.GetBytes(secret));

    public (string Token, DateTime ExpiresAt) Create(string username)
    {
        var expires = DateTime.UtcNow.AddHours(12);
        var creds = new SigningCredentials(Key(cfg["Jwt:Secret"]!), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            claims: new[] { new Claim(NameClaim, username), new Claim(RoleClaim, "admin") },
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
