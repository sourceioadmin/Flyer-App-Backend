using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using System.ComponentModel.DataAnnotations;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuthController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest? loginData)
    {
        if (loginData == null)
        {
            return BadRequest(new { message = "Invalid request body" });
        }

        var user = await _context.Users
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Email == loginData.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(loginData.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            role = user.Role.ToString(),
            companyId = user.CompanyId,
            companyName = user.Company?.Name
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registerData)
    {
        // Only allow Company role registration
        if (registerData.Role != UserRole.Company)
        {
            return BadRequest(new { message = "Invalid role. Only company registration is allowed." });
        }

        // Validate password requirements
        if (!IsValidPassword(registerData.Password))
        {
            return BadRequest(new { message = "Password must be at least 6 characters, contain at least one letter and one number" });
        }

        // Check if email already exists
        if (await _context.Users.AnyAsync(u => u.Email == registerData.Email))
        {
            return BadRequest(new { message = "Email already exists" });
        }

        // Check if company name already exists
        if (await _context.Companies.AnyAsync(c => c.Name == registerData.CompanyName))
        {
            return BadRequest(new { message = "Company name already exists" });
        }

        // Create the company first
        var company = new Company
        {
            Name = registerData.CompanyName,
            ContactEmail = registerData.Email // Use the user's email as contact email
        };

        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        // Create the user associated with the company
        var user = new User
        {
            Email = registerData.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerData.Password),
            Role = registerData.Role,
            CompanyId = company.Id
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Company registered successfully" });
    }

    private bool IsValidPassword(string password)
    {
        if (password.Length < 6)
            return false;

        bool hasLetter = password.Any(char.IsLetter);
        bool hasDigit = password.Any(char.IsDigit);

        return hasLetter && hasDigit;
    }
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Company; // Default to Company
}
