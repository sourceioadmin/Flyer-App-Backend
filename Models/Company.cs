namespace backend.Models;

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? GbpReviewLink { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    
    public ICollection<Flyer> Flyers { get; set; } = new List<Flyer>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<ReviewCustomer> ReviewCustomers { get; set; } = new List<ReviewCustomer>();
}
