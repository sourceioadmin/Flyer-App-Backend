using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class AddReviewCustomerDto
{
    [Required]
    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [Phone]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    public int CompanyId { get; set; }
}

public class ReviewCustomerResponseDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Day0Sent { get; set; }
    public bool Day1Sent { get; set; }
    public bool Day3Sent { get; set; }
    public bool IsActive { get; set; }
}
