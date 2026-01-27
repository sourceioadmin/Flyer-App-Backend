namespace backend.Models;

public class Flyer
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime ForDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ImagePath { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    public Company? Company { get; set; }
}
