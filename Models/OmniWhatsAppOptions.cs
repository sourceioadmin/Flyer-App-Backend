namespace backend.Models;

public class OmniWhatsAppOptions
{
    public const string SectionName = "OmniWhatsApp";

    public string BaseUrl { get; set; } = "https://alots.io/v20.0";
    public string PhoneNumberId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "en";

    public string Day0TemplateName { get; set; } = "service_feedback_request_1";
    public string Day1TemplateName { get; set; } = "service_feedback_reminder_2";
    public string Day3TemplateName { get; set; } = "service_feedback_final_reminder_3";
}
