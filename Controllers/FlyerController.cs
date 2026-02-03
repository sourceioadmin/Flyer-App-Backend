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
    private readonly ILogger<FlyerController> _logger;

    public FlyerController(AppDbContext context, IWebHostEnvironment environment, ILogger<FlyerController> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    private string? BuildPublicImageUrl(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return null;

        // If DB already has an absolute URL (future-proof for Blob/CDN), just return it.
        if (Uri.TryCreate(imagePath, UriKind.Absolute, out _)) return imagePath;

        if (!imagePath.StartsWith("/")) imagePath = "/" + imagePath;
        return $"{Request.Scheme}://{Request.Host}{imagePath}";
    }

    private string ResolveWebRootPath()
    {
        // Azure can sometimes have WebRootPath null depending on hosting model.
        return !string.IsNullOrEmpty(_environment.WebRootPath)
            ? _environment.WebRootPath
            : Path.Combine(_environment.ContentRootPath, "wwwroot");
    }

    // Diagnostic endpoint to check file storage location
    [HttpGet("diagnostics/storage-path")]
    public IActionResult GetStoragePath()
    {
        var webRootPath = _environment.WebRootPath ?? "NULL (WebRootPath is null!)";
        var contentRootPath = _environment.ContentRootPath;
        var uploadsPath = !string.IsNullOrEmpty(_environment.WebRootPath) 
            ? Path.Combine(_environment.WebRootPath, "uploads") 
            : "Cannot determine (WebRootPath is null)";
        
        var diagnostics = new
        {
            WebRootPath = webRootPath,
            ContentRootPath = contentRootPath,
            UploadsPath = uploadsPath,
            UploadsPathExists = !string.IsNullOrEmpty(_environment.WebRootPath) && Directory.Exists(uploadsPath),
            EnvironmentName = _environment.EnvironmentName
        };

        _logger.LogWarning("Storage Diagnostics: WebRootPath={WebRootPath}, UploadsPath={UploadsPath}, Exists={Exists}", 
            webRootPath, uploadsPath, diagnostics.UploadsPathExists);

        return Ok(diagnostics);
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

        // Determine upload path (handle null WebRootPath in Azure)
        string uploadsPath;
        string filePath;
        string uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
        bool usingFallback = false;

        if (string.IsNullOrEmpty(_environment.WebRootPath))
        {
            _logger.LogError("WebRootPath is null! Using ContentRootPath as fallback.");
            // Fallback to ContentRootPath/wwwroot/uploads
            var fallbackPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
            if (!Directory.Exists(fallbackPath))
            {
                Directory.CreateDirectory(fallbackPath);
            }
            uploadsPath = Path.Combine(fallbackPath, "uploads");
            usingFallback = true;
        }
        else
        {
            uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
        }

        if (!Directory.Exists(uploadsPath))
        {
            Directory.CreateDirectory(uploadsPath);
            _logger.LogInformation("Created uploads directory: {Path}", uploadsPath);
        }

        filePath = Path.Combine(uploadsPath, uniqueFileName);

        _logger.LogInformation("Saving file to: {FilePath} (Fallback: {UsingFallback})", filePath, usingFallback);

        // Save file
        try
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }
            _logger.LogInformation("File saved successfully: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file to {FilePath}", filePath);
            return StatusCode(500, new { message = "Error saving file", error = ex.Message, path = filePath });
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

        var response = new { 
            message = usingFallback 
                ? "Flyer uploaded successfully (using fallback path)" 
                : "Flyer uploaded successfully", 
            flyer,
            savedPath = filePath
        };

        if (usingFallback)
        {
            return Ok(new { 
                message = response.message,
                flyer = response.flyer,
                savedPath = response.savedPath,
                warning = "WebRootPath was null - file saved to ContentRootPath/wwwroot/uploads"
            });
        }

        return Ok(response);
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
                ImageUrl = BuildPublicImageUrl(f.ImagePath),
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
                ImageUrl = BuildPublicImageUrl(f.ImagePath),
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

            // Delete old file (only if it was a local relative path)
            if (!string.IsNullOrWhiteSpace(flyer.ImagePath) && !Uri.TryCreate(flyer.ImagePath, UriKind.Absolute, out _))
            {
                var oldFilePath = Path.Combine(ResolveWebRootPath(), flyer.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
            }

            // Save new file
            var uploadsPath = Path.Combine(ResolveWebRootPath(), "uploads");
            if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);
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

        if (string.IsNullOrWhiteSpace(flyer.ImagePath))
        {
            return NotFound(new { message = "File not found" });
        }

        // If the DB has an absolute URL (e.g., Blob), don't try to download from local disk.
        if (Uri.TryCreate(flyer.ImagePath, UriKind.Absolute, out _))
        {
            return BadRequest(new { message = "This flyer image is stored remotely; download via the ImageUrl instead." });
        }

        var filePath = Path.Combine(ResolveWebRootPath(), flyer.ImagePath.TrimStart('/'));
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
