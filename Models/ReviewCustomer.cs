namespace backend.Models;

public class ReviewCustomer
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Day0Sent { get; set; }
    public bool Day1Sent { get; set; }
    public bool Day3Sent { get; set; }
    public bool IsActive { get; set; } = true;
}
