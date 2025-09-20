using System.Text;
using System.Text.Json;

public class TelegramService
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;
    private readonly string _chatId;

    public TelegramService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _botToken = configuration["Telegram:BotToken"]!;
        _chatId = configuration["Telegram:ChatId"]!;
    }

    public async Task SendApprovalRequestAsync(string username, Guid requestId)
    {
        var message = $"⚠️ **Inicio de Sesión Sospechoso** ⚠️\n\n" +
                      $"El usuario **{username}** está intentando iniciar sesión.\n" +
                      $"Por favor, aprueba o rechaza el acceso.\n\n" +
                      $"ID de Solicitud: `{requestId}`";

        var payload = new
        {
            chat_id = _chatId,
            text = message,
            parse_mode = "Markdown",
            reply_markup = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "✅ Aprobar", callback_data = $"approve_{requestId}" },
                        new { text = "❌ Rechazar", callback_data = $"reject_{requestId}" }
                    }
                }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"https://api.telegram.org/bot{_botToken}/sendMessage", content);
        response.EnsureSuccessStatusCode();
    }
}