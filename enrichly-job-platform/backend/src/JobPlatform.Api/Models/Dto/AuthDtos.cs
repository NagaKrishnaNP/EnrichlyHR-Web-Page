using System.ComponentModel.DataAnnotations;

namespace JobPlatform.Api.Models.Dto;

public record RegisterRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8)] string Password,
    [property: Required, MinLength(1), MaxLength(100)] string DisplayName
);

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password
);

public record AuthResponse(string Token, DateTime ExpiresAtUtc, string Email, string DisplayName, Guid UserId);
