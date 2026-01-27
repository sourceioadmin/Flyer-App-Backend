namespace backend.DTOs;

public class MonthFilterDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int? CompanyId { get; set; }
}
