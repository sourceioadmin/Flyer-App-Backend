using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class FlyerUpdateDto
{
    [Required]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public DateTime ForDate { get; set; }
}
