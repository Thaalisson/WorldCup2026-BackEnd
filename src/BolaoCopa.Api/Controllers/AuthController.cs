using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace BolaoCopa.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IPoolRepository _pools;
    private readonly IConfiguration _config;

    public AuthController(IUserRepository users, IPoolRepository pools, IConfiguration config)
    {
        _users = users;
        _pools = pools;
        _config = config;
    }

    [HttpPost("register")]
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
        return Ok(new AuthResponse(GenerateToken(user), user.Id, user.Name, user.Email, user.IsAdmin));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _users.GetByEmailAsync(request.Email);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized("E-mail ou senha inválidos.");

        return Ok(new AuthResponse(GenerateToken(user), user.Id, user.Name, user.Email, user.IsAdmin));
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claimsList = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email)
        };
        if (user.IsAdmin)
            claimsList.Add(new Claim(ClaimTypes.Role, "Admin"));
        var claims = claimsList.ToArray();

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
