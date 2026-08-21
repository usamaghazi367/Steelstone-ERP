using ConstFire.Backend.Data;
using ConstFire.Backend.DTOs;
using ConstFire.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Services;

public class AuthService(AppDbContext context, IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await context.Users.AnyAsync(u => u.Email == email))
            throw new InvalidOperationException("Email is already registered.");

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Name = request.Name.Trim(),
            Role = string.IsNullOrWhiteSpace(request.Role) ? "User" : request.Role.Trim()
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        return BuildAuthResponse(user);
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        var user = await context.Users.FindAsync(userId);
        return user is null ? null : MapToDto(user);
    }

    private AuthResponse BuildAuthResponse(User user) =>
        new()
        {
            Token = jwtTokenService.GenerateToken(user),
            User = MapToDto(user)
        };

    private static UserDto MapToDto(User user) =>
        new()
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = user.Role
        };
}
