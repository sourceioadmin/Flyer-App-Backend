using Microsoft.Extensions.Options;
using backend.Models;

namespace backend.Services;

public class ReviewMessageService
{
    private readonly OmniWhatsAppOptions _options;

    public ReviewMessageService(IOptions<OmniWhatsAppOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Builds Message 1 (Day 0) - e.g. dreamers_solar_msg_1.
    /// Returns (templateName, bodyParams, buttonSuffix, headerImageLink, headerImageId, languageCode).
    /// </summary>
    public (string templateName, List<string> bodyParams, string? buttonSuffix, string? headerImageLink, string? headerImageId, string? languageCode) GetDay0Message(
        int customerId, string companyName, string gbpReviewLink)
    {
        // dreamers_solar_msg_1: header image + empty body, no button, language mr
        return (
            _options.Day0TemplateName,
            new List<string>(),
            null,
            null,
            null,
            null
        );
    }

    /// <summary>
    /// Builds Message 2 (follow-up) - gentle reminder.
    /// </summary>
    public (string templateName, List<string> bodyParams, string? buttonSuffix) GetDay1Message(
        int customerId, string companyName, string gbpReviewLink)
    {
        return (
            _options.Day1TemplateName,
            new List<string> { companyName, gbpReviewLink },
            customerId.ToString()
        );
    }

    /// <summary>
    /// Builds Message 3 (final) - last nudge.
    /// </summary>
    public (string templateName, List<string> bodyParams, string? buttonSuffix) GetDay3Message(
        int customerId, string companyName, string gbpReviewLink)
    {
        return (
            _options.Day3TemplateName,
            new List<string> { companyName, gbpReviewLink },
            customerId.ToString()
        );
    }
}
