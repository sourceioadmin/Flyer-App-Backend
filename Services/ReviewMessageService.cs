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
    /// Builds Message 1 (immediate) - warm thank-you + review request.
    /// Button goes to /r/{customerId} which redirects to the company's GbpReviewLink.
    /// </summary>
    public (string templateName, List<string> bodyParams, string? buttonSuffix) GetDay0Message(
        int customerId, string customerName, string companyName, string gbpReviewLink)
    {
        return (
            _options.Day0TemplateName,
            new List<string> { customerName, companyName, gbpReviewLink },
            customerId.ToString()
        );
    }

    /// <summary>
    /// Builds Message 2 (follow-up) - gentle reminder.
    /// </summary>
    public (string templateName, List<string> bodyParams, string? buttonSuffix) GetDay1Message(
        int customerId, string customerName, string companyName, string gbpReviewLink)
    {
        return (
            _options.Day1TemplateName,
            new List<string> { customerName, companyName, gbpReviewLink },
            customerId.ToString()
        );
    }

    /// <summary>
    /// Builds Message 3 (final) - last nudge.
    /// </summary>
    public (string templateName, List<string> bodyParams, string? buttonSuffix) GetDay3Message(
        int customerId, string customerName, string companyName, string gbpReviewLink)
    {
        return (
            _options.Day3TemplateName,
            new List<string> { customerName, companyName, gbpReviewLink },
            customerId.ToString()
        );
    }
}
