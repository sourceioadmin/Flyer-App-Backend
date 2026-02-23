using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using backend.Models;

namespace backend.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly OmniWhatsAppOptions _options;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(HttpClient httpClient, IOptions<OmniWhatsAppOptions> options,
        ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendTextMessageAsync(string phoneNumber, string text)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.PhoneNumberId))
        {
            _logger.LogWarning("WhatsApp API credentials not configured. Skipping text message to {Phone}", phoneNumber);
            return false;
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/{_options.PhoneNumberId}/messages";
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = phoneNumber,
            type = "text",
            text = new { body = text }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var jsonContent = JsonSerializer.Serialize(payload, jsonOptions);

        _logger.LogInformation("Sending WhatsApp text message to {Phone}: {Text}", phoneNumber, text);
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("WhatsApp text message sent successfully to {Phone}", phoneNumber);
                return true;
            }
            _logger.LogWarning("WhatsApp API returned {StatusCode} for text message: {Response}",
                (int)response.StatusCode, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp text message to {Phone}", phoneNumber);
            return false;
        }
    }

    public async Task<bool> SendTemplateMessageAsync(string phoneNumber, string templateName,
        List<string> bodyParameters, string? buttonUrlSuffix,
        string? headerImageLink = null, string? headerImageId = null, string? languageCode = null)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.PhoneNumberId))
        {
            _logger.LogWarning("WhatsApp API credentials not configured. Skipping message to {Phone} with template {Template}",
                phoneNumber, templateName);
            return false;
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/{_options.PhoneNumberId}/messages";

        var components = new List<object>();

        // Header image (for templates like dreamers_solar_msg_1) - must come first
        if (!string.IsNullOrWhiteSpace(headerImageLink) || !string.IsNullOrWhiteSpace(headerImageId))
        {
            object imagePayload = string.IsNullOrWhiteSpace(headerImageId)
                ? new { link = headerImageLink!.Trim() }
                : new { id = headerImageId!.Trim() };
            components.Add(new
            {
                type = "header",
                parameters = new[]
                {
                    new { type = "image", image = imagePayload }
                }
            });
        }

        // Body parameters (include empty body component when template has body with no params, e.g. dreamers_solar_msg_1)
        var bodyParams = bodyParameters.Select(p => new
        {
            type = "text",
            text = p
        }).ToArray();
        components.Add(new
        {
            type = "body",
            parameters = bodyParams
        });

        // Dynamic URL button parameter
        if (!string.IsNullOrWhiteSpace(buttonUrlSuffix))
        {
            components.Add(new
            {
                type = "button",
                sub_type = "url",
                index = "0",
                parameters = new[]
                {
                    new { type = "text", text = buttonUrlSuffix }
                }
            });
        }

        var lang = languageCode ?? _options.LanguageCode;
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = phoneNumber,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = lang },
                components
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var jsonContent = JsonSerializer.Serialize(payload, jsonOptions);

        _logger.LogInformation("Sending WhatsApp template '{Template}' to {Phone}", templateName, phoneNumber);
        _logger.LogDebug("WhatsApp API payload: {Payload}", jsonContent);

        // Retry once on transient failure
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("WhatsApp message sent successfully to {Phone} (template: {Template})",
                        phoneNumber, templateName);
                    return true;
                }

                // Debug: log API errors so you can see them in the console/Output window when running the app
                _logger.LogWarning("WhatsApp API error. StatusCode={StatusCode} Attempt={Attempt}. Full response: {Response}",
                    (int)response.StatusCode, attempt, responseBody);

                if (attempt < 2 && IsTransientError(response.StatusCode))
                {
                    await Task.Delay(2000);
                    continue;
                }

                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error sending WhatsApp message on attempt {Attempt} to {Phone}",
                    attempt, phoneNumber);

                if (attempt < 2)
                {
                    await Task.Delay(2000);
                    continue;
                }

                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout sending WhatsApp message on attempt {Attempt} to {Phone}",
                    attempt, phoneNumber);
                return false;
            }
        }

        return false;
    }

    private static bool IsTransientError(System.Net.HttpStatusCode statusCode)
    {
        return statusCode == System.Net.HttpStatusCode.TooManyRequests
            || statusCode == System.Net.HttpStatusCode.ServiceUnavailable
            || statusCode == System.Net.HttpStatusCode.GatewayTimeout
            || statusCode == System.Net.HttpStatusCode.InternalServerError;
    }
}
