using Google.Authenticator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureLoginApi.Pg.Data.Context;
using SecureLoginApi.Pg.DTOs;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize] 
public class MfaController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public MfaController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("setup")]
    public async Task<IActionResult> SetupMfa()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _context.Users.FindAsync(int.Parse(userId!));

        if (user is null) return NotFound();
        if (user.MfaEnabled) return BadRequest("MFA ya está activado.");

        var appName = _configuration["Jwt:Issuer"] ?? "SecureLoginApp";
        var tfa = new TwoFactorAuthenticator();
        var secretKey = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10);

        user.MfaSecretKey = secretKey;
        await _context.SaveChangesAsync();

        var setupInfo = tfa.GenerateSetupCode(appName, user.Username, secretKey, false, 3);

        return Ok(new
        {
            ManualEntryKey = setupInfo.ManualEntryKey,
            QrCodeImageUrl = setupInfo.QrCodeSetupImageUrl
        });
    }

    [HttpPost("enable")]
    public async Task<IActionResult> EnableMfa([FromBody] MfaEnableDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _context.Users.FindAsync(int.Parse(userId!));

        if (user is null) return NotFound();
        if (string.IsNullOrEmpty(user.MfaSecretKey)) return BadRequest("Primero debes configurar MFA.");

        var tfa = new TwoFactorAuthenticator();
        var isValid = tfa.ValidateTwoFactorPIN(user.MfaSecretKey, request.Code);

        if (!isValid)
        {
            return BadRequest("El código es inválido.");
        }

        user.MfaEnabled = true;
        await _context.SaveChangesAsync();

        return Ok(new { Message = "MFA activado correctamente." });
    }
}

