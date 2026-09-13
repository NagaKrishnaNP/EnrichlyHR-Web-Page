using JobPlatform.Api.Auth;
using JobPlatform.Api.Models.Dto;
using JobPlatform.Api.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(UserManager<ApplicationUser> userManager, IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Could not create account.", errors = result.Errors.Select(e => e.Description) });
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user);
        return Ok(new AuthResponse(token, expiresAt, user.Email!, user.DisplayName, user.Id));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user);
        return Ok(new AuthResponse(token, expiresAt, user.Email!, user.DisplayName, user.Id));
    }
}
