namespace SimilarProductsWinForms.Services;

using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class NotificationService
{
    private static readonly HttpClient HttpClient = new();

    public static async Task<(bool Success, string Message)> SendTelegramMessageAsync(string botToken, string chatId, string text)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            return (false, "Bot Token veya Chat ID boş olamaz.");
        }

        try
        {
            var url = $"https://api.telegram.org/bot{botToken.Trim()}/sendMessage";
            var payload = new
            {
                chat_id = chatId.Trim(),
                text = text,
                parse_mode = "HTML"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Telegram bildirimi başarıyla gönderildi!");
            }

            var err = await response.Content.ReadAsStringAsync();
            return (false, $"Telegram hatası ({response.StatusCode}): {err}");
        }
        catch (Exception ex)
        {
            return (false, $"Bağlantı hatası: {ex.Message}");
        }
    }

    public static async Task<(bool Success, string Message)> SendWhatsAppMessageAsync(string apiUrl, string token, string phone, string text)
    {
        if (string.IsNullOrWhiteSpace(apiUrl) || string.IsNullOrWhiteSpace(phone))
        {
            return (false, "WhatsApp API URL veya Telefon Numarası boş olamaz.");
        }

        try
        {
            var payload = new
            {
                token = token.Trim(),
                to = phone.Trim(),
                body = text
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(apiUrl.Trim(), content);
            if (response.IsSuccessStatusCode)
            {
                return (true, "WhatsApp bildirimi başarıyla gönderildi!");
            }

            var err = await response.Content.ReadAsStringAsync();
            return (false, $"WhatsApp hatası ({response.StatusCode}): {err}");
        }
        catch (Exception ex)
        {
            return (false, $"Bağlantı hatası: {ex.Message}");
        }
    }

    public static async Task DispatchNotificationAsync(NotificationSettings settings, string title, string messageBody, string categorySymbol = "🔔")
    {
        if (settings == null) return;

        var formattedMsg = $"<b>{categorySymbol} {title}</b>\n\n{messageBody}\n\n<i>⏱️ {DateTime.Now:dd.MM.yyyy HH:mm:ss} | Etsy Marketplace Engine</i>";

        if (settings.TelegramEnabled && !string.IsNullOrWhiteSpace(settings.TelegramBotToken) && !string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            _ = SendTelegramMessageAsync(settings.TelegramBotToken, settings.TelegramChatId, formattedMsg);
        }

        if (settings.WhatsAppEnabled && !string.IsNullOrWhiteSpace(settings.WhatsAppApiUrl) && !string.IsNullOrWhiteSpace(settings.WhatsAppPhone))
        {
            _ = SendWhatsAppMessageAsync(settings.WhatsAppApiUrl, settings.WhatsAppToken, settings.WhatsAppPhone, $"{categorySymbol} {title}\n\n{messageBody}");
        }

        await Task.CompletedTask;
    }
}
