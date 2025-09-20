
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
    private readonly TelegramService _telegramService;
    public AuthController(ApplicationDbContext context, IConfiguration configuration, TelegramService telegramService)
    {
        _context = context;
        _configuration = configuration;
        _telegramService = telegramService;
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

        bool isSuspiciousLogin = CheckIfLoginIsSuspicious(user, HttpContext);

        if (isSuspiciousLogin)
        {
            var approvalRequest = new LoginApprovalRequest { UserId = user.Id };
            _context.LoginApprovalRequests.Add(approvalRequest);
            await _context.SaveChangesAsync();

            await _telegramService.SendApprovalRequestAsync(user.Username, approvalRequest.Id);

            return Ok(new
            {
                ApprovalRequired = true,
                ApprovalRequestId = approvalRequest.Id
            });
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
    [HttpGet("approval-status/{id}")]
    public async Task<IActionResult> GetApprovalStatus(Guid id)
    {
        var request = await _context.LoginApprovalRequests.FindAsync(id);
        if (request is null) return NotFound();

        return Ok(new { Status = request.Status.ToString() });
    }

    private bool CheckIfLoginIsSuspicious(User user, HttpContext httpContext)
    {
        return false;
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