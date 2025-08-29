using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    [HttpGet]
    [Authorize] 
    public IActionResult GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
        var userName = User.FindFirstValue(ClaimTypes.Name);

        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(new { Id = userId, Username = userName });
    }
}