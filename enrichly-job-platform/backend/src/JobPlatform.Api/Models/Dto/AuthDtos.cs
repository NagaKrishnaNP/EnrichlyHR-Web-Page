using System.ComponentModel.DataAnnotations;

namespace JobPlatform.Api.Models.Dto;

public class RegisterRequest{
    [Required] 
    [EmailAddress] 
    public string Email { get; set; } = string.Empty;
    [Required] 
    [MinLength(8)] 
    public string Password { get; set; } = string.Empty;
    [Required] 
    [MinLength(1)] 
    [MaxLength(100)] 
    public string DisplayName { get; set; } = string.Empty;
}

public record LoginRequest{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public record AuthResponse(string Token, DateTime ExpiresAtUtc, string Email, string DisplayName, Guid UserId);
