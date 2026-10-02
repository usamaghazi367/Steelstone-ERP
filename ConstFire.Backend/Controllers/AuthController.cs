using System.IdentityModel.Tokens.Jwt;
using ConstFire.Backend.DTOs;
using ConstFire.Backend.Models;
using ConstFire.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConstFire.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var response = await authService.RegisterAsync(request);
        return Ok(response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var response = await authService.LoginAsync(request);
        var expiryMinutes = int.TryParse(HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["Jwt:ExpiryMinutes"], out var m) ? m : 60;
        Response.Cookies.Append(
            "steelstone_auth",
            response.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes),
            });
        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await authService.GetUserByIdAsync(userId);
        if (user is null)
            return NotFound();

        return Ok(user);
    }
}
