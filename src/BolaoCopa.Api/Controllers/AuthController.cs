using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace BolaoCopa.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IConfiguration _config;

    public AuthController(IUserRepository users, IRefreshTokenRepository refreshTokens, IConfiguration config)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _config = config;
    }

    [HttpPost("register")]
    [EnableRateLimiting("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Nome, e-mail e senha são obrigatórios.");

        if (request.Password.Length < 6)
            return BadRequest("A senha deve ter pelo menos 6 caracteres.");

        if (await _users.ExistsByEmailAsync(request.Email))
            return Conflict("E-mail já cadastrado.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHasher.Hash(request.Password)
        };

        await _users.AddAsync(user);
        var token = await SetAuthCookiesAsync(user);
        return Ok(new UserInfoResponse(user.Id, user.Name, user.Email, user.IsAdmin, token));
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _users.GetByEmailAsync(request.Email);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized("E-mail ou senha inválidos.");

        var token = await SetAuthCookiesAsync(user);
        return Ok(new UserInfoResponse(user.Id, user.Name, user.Email, user.IsAdmin, token));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var rawToken = Request.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(rawToken))
            return Unauthorized();

        var stored = await _refreshTokens.GetByTokenAsync(rawToken);
        if (stored is null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
        {
            ClearAuthCookies();
            return Unauthorized("Sessão expirada. Faça login novamente.");
        }

        var user = await _users.GetByIdAsync(stored.UserId);
        if (user is null)
        {
            ClearAuthCookies();
            return Unauthorized();
        }

        // Rotate: revoke old, issue new
        await _refreshTokens.RevokeAsync(stored);
        var token = await SetAuthCookiesAsync(user);
        return Ok(new { token });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var rawToken = Request.Cookies["refresh_token"];
        if (!string.IsNullOrEmpty(rawToken))
        {
            var stored = await _refreshTokens.GetByTokenAsync(rawToken);
            if (stored is not null) await _refreshTokens.RevokeAsync(stored);
        }

        ClearAuthCookies();
        return Ok();
    }

    private async Task<string> SetAuthCookiesAsync(User user)
    {
        var accessToken = GenerateAccessToken(user);

        Response.Cookies.Append("access_token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddHours(1),
            Path = "/"
        });

        var refreshRaw = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        await _refreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshRaw,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });

        Response.Cookies.Append("refresh_token", refreshRaw, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/api/auth"
        });

        return accessToken;
    }

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete("access_token", new CookieOptions
        { Secure = true, SameSite = SameSiteMode.None, Path = "/" });
        Response.Cookies.Delete("refresh_token", new CookieOptions
        { Secure = true, SameSite = SameSiteMode.None, Path = "/api/auth" });
    }

    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email)
        };
        if (user.IsAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record UserInfoResponse(Guid UserId, string Name, string Email, bool IsAdmin, string Token);
