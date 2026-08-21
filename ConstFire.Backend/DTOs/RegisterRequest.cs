using System.ComponentModel.DataAnnotations;

namespace ConstFire.Backend.DTOs;

public class RegisterRequest
{
    [Required, EmailAddress]
    public required string Email { get; set; }

    [Required, MinLength(6)]
    public required string Password { get; set; }

    [Required, MinLength(2)]
    public required string Name { get; set; }

    public string Role { get; set; } = "User";
}
