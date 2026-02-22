namespace backend.Services;

public interface IWhatsAppService
{
    /// <summary>
    /// Sends a simple text message via the Omni App (alots.io) API.
    /// Used to open the conversation before sending template messages (per Meta/Omni requirement).
    /// </summary>
    Task<bool> SendTextMessageAsync(string phoneNumber, string text);

    /// <summary>
    /// Sends a WhatsApp template message via the Omni App (alots.io) API.
    /// </summary>
    /// <param name="phoneNumber">Recipient phone number with country code (e.g., "919076006262")</param>
    /// <param name="templateName">Pre-approved WhatsApp template name</param>
    /// <param name="bodyParameters">Ordered list of body variable values ({{1}}, {{2}}, etc.)</param>
    /// <param name="buttonUrlSuffix">Dynamic URL suffix for the "Visit website" button</param>
    /// <param name="headerImageLink">Optional header image URL (for templates with image header).</param>
    /// <param name="headerImageId">Optional header image media ID (when image already uploaded to WhatsApp).</param>
    /// <param name="languageCode">Optional template language code override (e.g. "mr" for dreamers_solar_msg_1).</param>
    /// <returns>True if the message was sent successfully</returns>
    Task<bool> SendTemplateMessageAsync(string phoneNumber, string templateName,
        List<string> bodyParameters, string? buttonUrlSuffix,
        string? headerImageLink = null, string? headerImageId = null, string? languageCode = null);
}
