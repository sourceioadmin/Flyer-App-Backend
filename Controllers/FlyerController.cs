using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.DTOs;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlyerController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public FlyerController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // Upload flyer
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFlyer([FromForm] FlyerUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        // Validate file type
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
        var fileExtension = Path.GetExtension(request.File.FileName).ToLower();
        if (!allowedExtensions.Contains(fileExtension))
        {
            return BadRequest(new { message = "Only PNG and JPG files are allowed" });
        }

        // Create uploads directory if it doesn't exist
        var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
        if (!Directory.Exists(uploadsPath))
        {
            Directory.CreateDirectory(uploadsPath);
        }

        // Generate unique filename
        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
        var filePath = Path.Combine(uploadsPath, uniqueFileName);

        // Save file
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await request.File.CopyToAsync(stream);
        }

        // Save flyer metadata to database
        var flyer = new Flyer
        {
            Title = request.Title,
            ForDate = request.ForDate,
            ImagePath = $"/uploads/{uniqueFileName}",
            CompanyId = request.CompanyId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Flyers.Add(flyer);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Flyer uploaded successfully", flyer });
    }

    // Get flyers for a specific company with optional month filter
    [HttpGet("company/{companyId}")]
    public async Task<IActionResult> GetFlyersByCompany(int companyId, [FromQuery] int? year, [FromQuery] int? month)
    {
        var query = _context.Flyers.Where(f => f.CompanyId == companyId);

        // Apply month filter if provided
        if (year.HasValue && month.HasValue)
        {
            query = query.Where(f => f.ForDate.Year == year.Value && f.ForDate.Month == month.Value);
        }

        var flyers = await query
            .OrderByDescending(f => f.ForDate)
            .Select(f => new
            {
                f.Id,
                f.Title,
                f.ImagePath,
                f.CompanyId,
                f.ForDate,
                f.CreatedAt
            })
            .ToListAsync();

        return Ok(flyers);
    }

    // Get all flyers with optional company and month filters (for admin)
    [HttpGet]
    public async Task<IActionResult> GetFlyers([FromQuery] int? companyId, [FromQuery] int? year, [FromQuery] int? month)
    {
        var query = _context.Flyers.AsQueryable();

        // Apply company filter if provided
        if (companyId.HasValue)
        {
            query = query.Where(f => f.CompanyId == companyId.Value);
        }

        // Apply month filter if provided
        if (year.HasValue && month.HasValue)
        {
            query = query.Where(f => f.ForDate.Year == year.Value && f.ForDate.Month == month.Value);
        }

        var flyers = await query
            .Include(f => f.Company)
            .OrderByDescending(f => f.ForDate)
            .Select(f => new
            {
                f.Id,
                f.Title,
                f.ImagePath,
                f.CompanyId,
                CompanyName = f.Company!.Name,
                f.ForDate,
                f.CreatedAt
            })
            .ToListAsync();

        return Ok(flyers);
    }

    // Update flyer (title, forDate, and optionally image)
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateFlyer(int id, [FromForm] FlyerUpdateRequest request)
    {
        var flyer = await _context.Flyers.FindAsync(id);
        if (flyer == null)
        {
            return NotFound(new { message = "Flyer not found" });
        }

        flyer.Title = request.Title;
        flyer.ForDate = request.ForDate;

        // If a new image is provided, replace the old one
        if (request.File != null && request.File.Length > 0)
        {
            // Validate file type
            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
            var fileExtension = Path.GetExtension(request.File.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { message = "Only PNG and JPG files are allowed" });
            }

            // Delete old file
            var oldFilePath = Path.Combine(_environment.WebRootPath, flyer.ImagePath.TrimStart('/'));
            if (System.IO.File.Exists(oldFilePath))
            {
                System.IO.File.Delete(oldFilePath);
            }

            // Save new file
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var newFilePath = Path.Combine(uploadsPath, uniqueFileName);

            using (var stream = new FileStream(newFilePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            flyer.ImagePath = $"/uploads/{uniqueFileName}";
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Flyer updated successfully", flyer });
    }

    // Download flyer
    [HttpGet("download/{id}")]
    public async Task<IActionResult> DownloadFlyer(int id)
    {
        var flyer = await _context.Flyers.FindAsync(id);
        if (flyer == null)
        {
            return NotFound(new { message = "Flyer not found" });
        }

        var filePath = Path.Combine(_environment.WebRootPath, flyer.ImagePath.TrimStart('/'));
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "File not found" });
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        var fileName = Path.GetFileName(filePath);
        return File(fileBytes, "image/jpeg", fileName);
    }

    // Delete flyer (soft delete)
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFlyer(int id)
    {
        var flyer = await _context.Flyers.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Id == id);
        if (flyer == null)
        {
            return NotFound(new { message = "Flyer not found" });
        }

        // Soft delete - just mark as deleted
        flyer.IsDeleted = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Flyer deleted successfully" });
    }
}

public class FlyerUploadRequest
{
    public string Title { get; set; } = string.Empty;
    public DateTime ForDate { get; set; }
    public int CompanyId { get; set; }
    public IFormFile? File { get; set; }
}

public class FlyerUpdateRequest
{
    public string Title { get; set; } = string.Empty;
    public DateTime ForDate { get; set; }
    public IFormFile? File { get; set; }
}
