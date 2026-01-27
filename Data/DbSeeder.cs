using backend.Models;

namespace backend.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        // Check if data already exists
        if (context.Companies.Any())
        {
            return; // Database already seeded
        }

        // Create companies
        var companies = new List<Company>
        {
            new Company { Name = "Company A", ContactEmail = "contact@companya.com" },
            new Company { Name = "Company B", ContactEmail = "contact@companyb.com" },
            new Company { Name = "Company C", ContactEmail = "contact@companyc.com" }
        };

        context.Companies.AddRange(companies);
        context.SaveChanges();

        // Create admin user (Password: admin123)
        var admin = new User
        {
            Email = "admin@flyer.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = UserRole.Admin,
            CompanyId = null
        };

        // Create company users (Password: company123)
        var companyUsers = new List<User>
        {
            new User
            {
                Email = "companyA@flyer.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("company123"),
                Role = UserRole.Company,
                CompanyId = companies[0].Id
            },
            new User
            {
                Email = "companyB@flyer.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("company123"),
                Role = UserRole.Company,
                CompanyId = companies[1].Id
            },
            new User
            {
                Email = "companyC@flyer.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("company123"),
                Role = UserRole.Company,
                CompanyId = companies[2].Id
            }
        };

        context.Users.Add(admin);
        context.Users.AddRange(companyUsers);
        context.SaveChanges();
    }
}
