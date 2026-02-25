using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(AppDbContext context, IWhatsAppService whatsAppService,
        ReviewMessageService messageService,
        ILogger<ReviewController> logger)
    {
        _context = context;
        _whatsAppService = whatsAppService;
        _messageService = messageService;
        _logger = logger;
    }

    /// <summary>
    /// Add one or more customers (comma-separated phone numbers) and send the first review request message (Day 0) to each.
    /// </summary>
    [HttpPost("customer")]
    public async Task<IActionResult> AddCustomer([FromBody] AddReviewCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var rawNumbers = dto.PhoneNumber
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        if (rawNumbers.Count == 0)
        {
            return BadRequest(new { message = "At least one phone number is required. Provide a single number or comma-separated values (e.g. 9876543210, 9876543211)." });
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

        var result = new AddReviewCustomersResultDto();

        foreach (var raw in rawNumbers)
        {
            var normalizedPhone = NormalizeIndianPhoneNumber(raw);
            if (normalizedPhone == null)
            {
                result.Invalid.Add(raw);
                continue;
            }

            var alreadyExists = await _context.ReviewCustomers
                .IgnoreQueryFilters()
                .AnyAsync(rc => rc.CompanyId == dto.CompanyId && rc.PhoneNumber == normalizedPhone);
            if (alreadyExists)
            {
                result.Duplicates.Add(raw);
                continue;
            }

            var customer = new ReviewCustomer
            {
                PhoneNumber = normalizedPhone,
                CompanyId = dto.CompanyId,
                IsActive = true
            };

            _context.ReviewCustomers.Add(customer);
            await _context.SaveChangesAsync();

            var (templateName, bodyParams, buttonSuffix, headerImageLink, headerImageId, languageCode) = _messageService.GetDay0Message(
                customer.Id, company.Name, company.GbpReviewLink!);

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

            result.Added.Add(MapToResponse(customer));
        }

        return StatusCode(201, result);
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
            PhoneNumber = customer.PhoneNumber,
            CompanyId = customer.CompanyId,
            CreatedAt = customer.CreatedAt,
            Day0Sent = customer.Day0Sent,
            Day1Sent = customer.Day1Sent,
            Day3Sent = customer.Day3Sent,
            IsActive = customer.IsActive
        };
    }

    /// <summary>
    /// Normalizes Indian phone number to E.164-style format with country code 91.
    /// Accepts 10 digits (prepends 91) or 12 digits starting with 91 (returns as-is).
    /// </summary>
    private static string? NormalizeIndianPhoneNumber(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length == 10)
            return "91" + digits;
        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
            return digits;
        return null;
    }
}
