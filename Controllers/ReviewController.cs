using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ReviewMessageService _messageService;
    private readonly OmniWhatsAppOptions _omniOptions;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(AppDbContext context, IWhatsAppService whatsAppService,
        ReviewMessageService messageService, IOptions<OmniWhatsAppOptions> omniOptions,
        ILogger<ReviewController> logger)
    {
        _context = context;
        _whatsAppService = whatsAppService;
        _messageService = messageService;
        _omniOptions = omniOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Add a new customer and send the first review request message (Day 0).
    /// </summary>
    [HttpPost("customer")]
    public async Task<IActionResult> AddCustomer([FromBody] AddReviewCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var company = await _context.Companies.FindAsync(dto.CompanyId);
        if (company == null)
        {
            return NotFound(new { message = "Company not found" });
        }

        if (string.IsNullOrWhiteSpace(company.GbpReviewLink))
        {
            return BadRequest(new { message = "Company does not have a GBP review link configured. Please update the company's GBP review link first." });
        }

        // Ensure the same phone number is not added twice for this company (one review flow per customer per company)
        var alreadyExists = await _context.ReviewCustomers
            .IgnoreQueryFilters()
            .AnyAsync(rc => rc.CompanyId == dto.CompanyId && rc.PhoneNumber == dto.PhoneNumber);
        if (alreadyExists)
        {
            return Conflict(new { message = "A review customer with this phone number already exists for this company. Each customer should be added only once per company." });
        }

        var customer = new ReviewCustomer
        {
            CustomerName = dto.CustomerName,
            PhoneNumber = dto.PhoneNumber,
            CompanyId = dto.CompanyId,
            IsActive = true
        };

        _context.ReviewCustomers.Add(customer);
        await _context.SaveChangesAsync();

        // Optionally send "Hi" first (can trigger 131049 if recipient has not opted in; disable by default)
        if (_omniOptions.SendHiBeforeTemplate)
        {
            await _whatsAppService.SendTextMessageAsync(customer.PhoneNumber, "Hi");
            await Task.Delay(2000);
        }

        var (templateName, bodyParams, buttonSuffix, headerImageLink, headerImageId, languageCode) = _messageService.GetDay0Message(
            customer.Id, customer.CustomerName, company.Name, company.GbpReviewLink!);

        var sent = await _whatsAppService.SendTemplateMessageAsync(
            customer.PhoneNumber, templateName, bodyParams, buttonSuffix, headerImageLink, headerImageId, languageCode);

        if (sent)
        {
            customer.Day0Sent = true;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Day 0 message sent to customer {CustomerId} ({Phone})",
                customer.Id, customer.PhoneNumber);
        }
        else
        {
            _logger.LogWarning("Failed to send Day 0 message to customer {CustomerId} ({Phone}). Will retry on next scheduler run.",
                customer.Id, customer.PhoneNumber);
        }

        return Ok(MapToResponse(customer));
    }

    /// <summary>
    /// Get all review customers for a company.
    /// </summary>
    [HttpGet("customers/{companyId}")]
    public async Task<IActionResult> GetByCompany(int companyId)
    {
        var companyExists = await _context.Companies.AnyAsync(c => c.Id == companyId);
        if (!companyExists)
        {
            return NotFound(new { message = "Company not found" });
        }

        var customers = await _context.ReviewCustomers
            .Where(rc => rc.CompanyId == companyId)
            .OrderByDescending(rc => rc.CreatedAt)
            .Select(rc => new ReviewCustomerResponseDto
            {
                Id = rc.Id,
                CustomerName = rc.CustomerName,
                PhoneNumber = rc.PhoneNumber,
                CompanyId = rc.CompanyId,
                CreatedAt = rc.CreatedAt,
                Day0Sent = rc.Day0Sent,
                Day1Sent = rc.Day1Sent,
                Day3Sent = rc.Day3Sent,
                IsActive = rc.IsActive
            })
            .ToListAsync();

        return Ok(customers);
    }

    /// <summary>
    /// Get a single review customer by ID.
    /// </summary>
    [HttpGet("customer/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _context.ReviewCustomers
            .FirstOrDefaultAsync(rc => rc.Id == id);

        if (customer == null)
        {
            return NotFound(new { message = "Review customer not found" });
        }

        return Ok(MapToResponse(customer));
    }

    /// <summary>
    /// Deactivate a review customer (stops future automated messages).
    /// </summary>
    [HttpDelete("customer/{id}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var customer = await _context.ReviewCustomers.FindAsync(id);
        if (customer == null)
        {
            return NotFound(new { message = "Review customer not found" });
        }

        customer.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Review customer {CustomerId} deactivated. Future messages stopped.", id);

        return Ok(new { message = "Customer deactivated. No further review messages will be sent." });
    }

    private static ReviewCustomerResponseDto MapToResponse(ReviewCustomer customer)
    {
        return new ReviewCustomerResponseDto
        {
            Id = customer.Id,
            CustomerName = customer.CustomerName,
            PhoneNumber = customer.PhoneNumber,
            CompanyId = customer.CompanyId,
            CreatedAt = customer.CreatedAt,
            Day0Sent = customer.Day0Sent,
            Day1Sent = customer.Day1Sent,
            Day3Sent = customer.Day3Sent,
            IsActive = customer.IsActive
        };
    }
}
