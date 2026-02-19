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

    public string Day0TemplateName { get; set; } = "review_request_day0";
    public string Day1TemplateName { get; set; } = "review_reminder_day1";
    public string Day3TemplateName { get; set; } = "review_reminder_day3";
}
