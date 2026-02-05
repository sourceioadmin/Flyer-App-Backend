using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlyerController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FlyerController> _logger;
    private readonly BlobService _blobService;

    public FlyerController(AppDbContext context, IWebHostEnvironment environment, ILogger<FlyerController> logger, BlobService blobService)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
        _blobService = blobService;
    }

    private string? BuildPublicImageUrl(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return null;

        // Legacy/absolute URL case – just return as-is.
        if (Uri.TryCreate(imagePath, UriKind.Absolute, out _)) return imagePath;

        // New behavior: treat non-absolute ImagePath as a blob name and return a read-only SAS URL.
        try
        {
            return _blobService.GetReadSasUrl(imagePath, TimeSpan.FromMinutes(30));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SAS URL for imagePath '{ImagePath}'", imagePath);
            return null;
        }
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

    // Upload flyer to Azure Blob Storage, store blob name in DB, and return SAS URL
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

        string blobName;
        string flyerUrl;
        try
        {
            // Upload and get the blob name to store in DB
            blobName = await _blobService.UploadAsync(request.File);

            // Generate a short-lived read-only SAS URL for the client
            flyerUrl = _blobService.GetReadSasUrl(blobName, TimeSpan.FromHours(1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to Azure Blob Storage");
            return StatusCode(500, new { message = "Error uploading file to storage", error = ex.Message });
        }

        // Save flyer metadata to database; ImagePath now stores the blob name
        var flyer = new Flyer
        {
            Title = request.Title,
            ForDate = request.ForDate,
            ImagePath = blobName,
            CompanyId = request.CompanyId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Flyers.Add(flyer);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Flyer uploaded successfully",
            flyerUrl,
            flyer
        });
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

        // IMPORTANT: keep EF query SQL-translatable; build ImageUrl after materialization.
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

        var response = flyers.Select(f => new
        {
            f.Id,
            f.Title,
            f.ImagePath,
            ImageUrl = BuildPublicImageUrl(f.ImagePath),
            f.CompanyId,
            f.ForDate,
            f.CreatedAt
        });

        return Ok(response);
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

        // IMPORTANT: keep EF query SQL-translatable; build ImageUrl after materialization.
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

        var response = flyers.Select(f => new
        {
            f.Id,
            f.Title,
            f.ImagePath,
            ImageUrl = BuildPublicImageUrl(f.ImagePath),
            f.CompanyId,
            f.CompanyName,
            f.ForDate,
            f.CreatedAt
        });

        return Ok(response);
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

    // Download flyer (supports both local files and Azure Blob Storage)
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

        try
        {
            // Check if ImagePath is an absolute URL (legacy Azure Blob Storage URL)
            if (Uri.TryCreate(flyer.ImagePath, UriKind.Absolute, out _))
            {
                // Legacy case: ImagePath contains full URL - redirect to it
                // (This won't help with CORS, but maintains backward compatibility)
                _logger.LogWarning("Flyer {FlyerId} uses legacy absolute URL storage. Consider migrating to blob name storage.", id);
                return Redirect(flyer.ImagePath);
            }

            // New case: ImagePath is a blob name - download from Azure Blob Storage
            try
            {
                var stream = await _blobService.DownloadAsync(flyer.ImagePath);
                var extension = Path.GetExtension(flyer.ImagePath)?.ToLowerInvariant() ?? ".jpg";
                var mimeType = extension == ".png" ? "image/png" : "image/jpeg";
                var fileName = $"{flyer.Title.Replace(" ", "_")}{extension}";
                
                return File(stream, mimeType, fileName);
            }
            catch (Exception blobEx)
            {
                _logger.LogError(blobEx, "Failed to download blob '{ImagePath}' for flyer {FlyerId}", flyer.ImagePath, id);
                
                // Fallback: Try local file system (for legacy flyers or development)
                var filePath = Path.Combine(ResolveWebRootPath(), flyer.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                    var fileName = Path.GetFileName(filePath);
                    var extension = Path.GetExtension(filePath)?.ToLowerInvariant() ?? ".jpg";
                    var mimeType = extension == ".png" ? "image/png" : "image/jpeg";
                    return File(fileBytes, mimeType, fileName);
                }
                
                return NotFound(new { message = "File not found in blob storage or local file system" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading flyer {FlyerId}", id);
            return StatusCode(500, new { message = "Error downloading file", error = ex.Message });
        }
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
