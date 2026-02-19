using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class CompanyDto
{
    public int? Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(255)]
    [EmailAddress]
    public string? ContactEmail { get; set; }
    
    [MaxLength(500)]
    [Url]
    public string? GbpReviewLink { get; set; }
}
