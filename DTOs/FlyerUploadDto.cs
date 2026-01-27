using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class FlyerUploadDto
{
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public DateTime ForDate { get; set; }
    
    [Required]
    public int CompanyId { get; set; }
}
