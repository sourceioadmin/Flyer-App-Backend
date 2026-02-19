using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.DTOs;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompanyController : ControllerBase
{
    private readonly AppDbContext _context;

    public CompanyController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var companies = await _context.Companies
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.ContactEmail,
                c.GbpReviewLink,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(companies);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var company = await _context.Companies
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.ContactEmail,
                c.GbpReviewLink,
                c.CreatedAt
            })
            .FirstOrDefaultAsync(c => c.Id == id);

        if (company == null)
        {
            return NotFound(new { message = "Company not found" });
        }

        return Ok(company);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CompanyDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Check if company name already exists
        if (await _context.Companies.AnyAsync(c => c.Name == dto.Name))
        {
            return BadRequest(new { message = "Company name already exists" });
        }

        var company = new Company
        {
            Name = dto.Name,
            ContactEmail = dto.ContactEmail,
            GbpReviewLink = dto.GbpReviewLink
        };

        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = company.Id,
            name = company.Name,
            contactEmail = company.ContactEmail,
            gbpReviewLink = company.GbpReviewLink,
            createdAt = company.CreatedAt
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CompanyDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var company = await _context.Companies.FindAsync(id);

        if (company == null)
        {
            return NotFound(new { message = "Company not found" });
        }

        // Check if new name conflicts with another company
        if (await _context.Companies.AnyAsync(c => c.Name == dto.Name && c.Id != id))
        {
            return BadRequest(new { message = "Company name already exists" });
        }

        company.Name = dto.Name;
        company.ContactEmail = dto.ContactEmail;
        company.GbpReviewLink = dto.GbpReviewLink;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = company.Id,
            name = company.Name,
            contactEmail = company.ContactEmail,
            gbpReviewLink = company.GbpReviewLink,
            createdAt = company.CreatedAt
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var company = await _context.Companies.FindAsync(id);

        if (company == null)
        {
            return NotFound(new { message = "Company not found" });
        }

        // Soft delete
        company.IsActive = false;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Company deleted successfully" });
    }
}
