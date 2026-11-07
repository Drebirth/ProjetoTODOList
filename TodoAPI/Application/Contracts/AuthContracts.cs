using System.ComponentModel.DataAnnotations;

namespace TodoAPI.Application.Contracts;

public sealed class RegisterRequest
{
    [Required]
    [StringLength(50)]
    [NotWhiteSpace]
    public string Username { get; init; } = string.Empty;

    [Required]
    [NotWhiteSpace]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required]
    [StringLength(50)]
    [NotWhiteSpace]
    public string Username { get; init; } = string.Empty;

    [Required]
    [NotWhiteSpace]
    public string Password { get; init; } = string.Empty;
}

public sealed record RegisteredUserResponse(int Id, string Username);

public sealed record LoginResponse(string Token, string TokenType, DateTime ExpiresAtUtc);

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotWhiteSpaceAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is string text && !string.IsNullOrWhiteSpace(text);
}
