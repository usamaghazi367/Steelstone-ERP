using ConstFire.Backend.Models;

namespace ConstFire.Backend.Services;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
