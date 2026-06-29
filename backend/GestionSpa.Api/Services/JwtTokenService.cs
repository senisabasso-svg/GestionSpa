using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestionSpa.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace GestionSpa.Api.Services;

public class JwtTokenService(IConfiguration config)
{
    public string GenerateToken(Usuario usuario)
    {
        var key = GetSigningKey();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Name, usuario.Nombre),
            new(ClaimTypes.Role, usuario.Rol.ToString()),
        };

        if (usuario.EmisorId.HasValue)
            claims.Add(new Claim("emisorId", usuario.EmisorId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: "GestionSpa",
            audience: "GestionSpa",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public SymmetricSecurityKey GetSigningKey()
    {
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? config["Jwt:Secret"]
            ?? "GestionSpa-Dev-Secret-Change-In-Production-32chars!";
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }
}

public static class PasswordHasher
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public static bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
