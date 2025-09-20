using Microsoft.AspNetCore.Mvc;
using SecureLoginApi.Pg.Data.Context;
using SecureLoginApi.Pg.Models;
using System.Text.Json.Serialization;

[ApiController]
[Route("api/[controller]")]
public class TelegramWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TelegramWebhookController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] TelegramUpdate update)
    {
        if (update.CallbackQuery is null || string.IsNullOrEmpty(update.CallbackQuery.Data)) return Ok();

        var parts = update.CallbackQuery.Data.Split('_');
        if (parts.Length != 2) return BadRequest();

        var action = parts[0];
        if (!Guid.TryParse(parts[1], out var requestId)) return BadRequest();

        var request = await _context.LoginApprovalRequests.FindAsync(requestId);
        if (request is null || request.Status != ApprovalStatus.Pending) return Ok();

        if (action == "approve")
        {
            if (!request.IsApprovedByPerson1) request.IsApprovedByPerson1 = true;
            else if (!request.IsApprovedByPerson2) request.IsApprovedByPerson2 = true;

            if (request.IsApprovedByPerson1 && request.IsApprovedByPerson2)
            {
                request.Status = ApprovalStatus.Approved;
            }
        }
        else if (action == "reject")
        {
            request.Status = ApprovalStatus.Rejected;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
    /*[HttpPost]
    public async Task<IActionResult> Post()
    {
        // Lee el cuerpo de la petición como un simple texto
        using var reader = new StreamReader(Request.Body);
        var rawJsonBody = await reader.ReadToEndAsync();

        // Imprime el contenido en la consola donde ejecutas "dotnet run"
        Console.WriteLine("=============================================");
        Console.WriteLine("  PAQUETE DE TELEGRAM RECIBIDO A LAS: " + DateTime.Now);
        Console.WriteLine("---------------------------------------------");
        Console.WriteLine(rawJsonBody);
        Console.WriteLine("=============================================");

        // Responde siempre con "OK" para que Telegram sepa que lo recibimos
        return Ok();
    }*/
}

public class TelegramUpdate
{
    [JsonPropertyName("callback_query")] // <-- Añadir atributo
    public CallbackQuery? CallbackQuery { get; set; }
}
public class CallbackQuery
{
    [JsonPropertyName("data")] // <-- Añadir atributo
    public string? Data { get; set; }
}