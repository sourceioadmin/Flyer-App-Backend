using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;

namespace backend.Controllers;

/// <summary>
/// Public redirect endpoint for WhatsApp review button clicks.
/// URL: /r/{id} -> 302 redirect to the company's GBP review page.
/// </summary>
[ApiController]
public class ReviewRedirectController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ReviewRedirectController> _logger;

    public ReviewRedirectController(AppDbContext context, ILogger<ReviewRedirectController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("/r/{id}")]
    public async Task<IActionResult> RedirectToReview(int id)
    {
        // Use IgnoreQueryFilters to find the customer even if deactivated
        var customer = await _context.ReviewCustomers
            .IgnoreQueryFilters()
            .Include(rc => rc.Company)
            .FirstOrDefaultAsync(rc => rc.Id == id);

        if (customer == null)
        {
            _logger.LogWarning("Review redirect: customer {Id} not found", id);
            return NotFound(new { message = "Review link not found" });
        }

        var gbpLink = customer.Company?.GbpReviewLink;

        if (string.IsNullOrWhiteSpace(gbpLink))
        {
            _logger.LogWarning("Review redirect: company {CompanyId} has no GBP review link", customer.CompanyId);
            return NotFound(new { message = "Review link not configured" });
        }

        _logger.LogInformation("Review redirect: customer {CustomerId} clicked review link, redirecting to {GbpLink}",
            id, gbpLink);

        return Redirect(gbpLink);
    }
}
