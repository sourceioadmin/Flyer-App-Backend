namespace backend.Models;

public class OmniWhatsAppOptions
{
    public const string SectionName = "OmniWhatsApp";

    public string BaseUrl { get; set; } = "https://alots.io/v20.0";
    public string PhoneNumberId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "en";
    /// <summary>
    /// If true, send a "Hi" text message before the Day 0 template (can trigger 131049 if recipient has not opted in).
    /// Default false to avoid "ecosystem engagement" rejections.
    /// </summary>
    public bool SendHiBeforeTemplate { get; set; }

    public string Day0TemplateName { get; set; } = "dreamers_solar_msg_1";
    /// <summary>Language code for Day 0 template (e.g. "mr" for dreamers_solar_msg_1).</summary>
    public string Day0TemplateLanguageCode { get; set; } = "mr";
    /// <summary>Optional header image URL for Day 0 template (dreamers_solar_msg_1). Use either this or Day0HeaderImageId.</summary>
    public string? Day0HeaderImageLink { get; set; }
    /// <summary>Optional header image media ID for Day 0 template. Use when image is already uploaded to WhatsApp.</summary>
    public string? Day0HeaderImageId { get; set; }
    public string Day1TemplateName { get; set; } = "review_reminder_day1";
    public string Day3TemplateName { get; set; } = "review_reminder_day3";
}
