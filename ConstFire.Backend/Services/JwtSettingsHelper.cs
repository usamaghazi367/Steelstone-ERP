using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ConstFire.Backend.Services;

public static class JwtSettingsHelper
{
    public static TokenValidationParameters CreateValidationParameters(IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt");
        var keyText = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText)),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(2),
        };
    }
}
