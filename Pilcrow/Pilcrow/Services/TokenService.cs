using Microsoft.IdentityModel.Tokens;
using Pilcrow.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Pilcrow.Services;

public class TokenService(IConfiguration config)
{
    private readonly string _key = config["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key is not configured.");

    public string CreateAccessToken(User user)
    {
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            ],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // The raw token goes to the client; only its hash is stored.
    public static (string raw, string hash) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var raw = Convert.ToBase64String(bytes);
        return (raw, Hash(raw));
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}