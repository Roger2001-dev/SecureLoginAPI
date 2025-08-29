
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SecureLoginApi.Pg.Data.Context;
using SecureLoginApi.Pg.DTOs;
using SecureLoginApi.Pg.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

[ApiController]
[Route("api/[controller]")] 
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("register")] 
    public async Task<IActionResult> Register(UserDto request)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Username == request.Username);
        if (userExists)
        {
            return Conflict("El nombre de usuario ya existe.");
        }

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Register), new { id = user.Id }, new { user.Id, user.Username });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(UserDto request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized("Credenciales inválidas.");
        }

        if (user.MfaEnabled)
        {
            return Ok(new { MfaRequired = true });
        }

        var accessToken = CreateJwtToken(user);
        var refreshToken = GenerateRefreshToken();
        await SetRefreshToken(refreshToken, user);

        return Ok(new { AccessToken = accessToken, RefreshToken = refreshToken.Token });
    }
    [HttpPost("verify-mfa")]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyDto request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null || string.IsNullOrEmpty(user.MfaSecretKey))
        {
            return Unauthorized("Usuario no encontrado o MFA no configurado.");
        }

        var tfa = new Google.Authenticator.TwoFactorAuthenticator();
        var isValid = tfa.ValidateTwoFactorPIN(user.MfaSecretKey, request.Code);

        if (!isValid)
        {
            return Unauthorized("El código MFA es inválido.");
        }

        var accessToken = CreateJwtToken(user);
        var refreshToken = GenerateRefreshToken();
        await SetRefreshToken(refreshToken, user);

        return Ok(new { AccessToken = accessToken, RefreshToken = refreshToken.Token });
    }

    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto request)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

        if (user is null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return Unauthorized("Refresh token inválido o expirado.");
        }

        var newAccessToken = CreateJwtToken(user);
        var newRefreshToken = GenerateRefreshToken();
        await SetRefreshToken(newRefreshToken, user);

        return Ok(new { AccessToken = newAccessToken, RefreshToken = newRefreshToken.Token });
    }
    private string CreateJwtToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), 
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    private static RefreshToken GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);

        return new RefreshToken
        {
            Token = Convert.ToBase64String(randomNumber),
            Expires = DateTime.UtcNow.AddDays(7), 
            Created = DateTime.UtcNow
        };
    }

    private async Task SetRefreshToken(RefreshToken newRefreshToken, User user)
    {
        user.RefreshToken = newRefreshToken.Token;
        user.RefreshTokenExpiryTime = newRefreshToken.Expires;
        _context.Update(user);
        await _context.SaveChangesAsync();
    }

}