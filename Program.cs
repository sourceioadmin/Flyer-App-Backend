using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using backend.Data;
using backend.Models;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure to listen on all network interfaces with HTTPS
builder.WebHost.UseUrls("https://0.0.0.0:5001");

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Use property names as-is
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure SQL Server database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register BlobService for Azure Blob Storage uploads
builder.Services.AddSingleton<BlobService>();

// Review Box: Strongly-typed options
builder.Services.Configure<OmniWhatsAppOptions>(
    builder.Configuration.GetSection(OmniWhatsAppOptions.SectionName));
builder.Services.Configure<ReviewScheduleOptions>(
    builder.Configuration.GetSection(ReviewScheduleOptions.SectionName));

// Review Box: WhatsApp service with HttpClient
builder.Services.AddHttpClient<WhatsAppService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();

// Review Box: Message template service and background scheduler
builder.Services.AddScoped<ReviewMessageService>();
builder.Services.AddHostedService<ReviewSchedulerService>();

// Configure CORS for React frontend (allow any origin for development)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .WithExposedHeaders("Content-Disposition");
        });
});

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    DbSeeder.Seed(context);
}

// Configure Azure Blob Storage CORS (one-time setup)
try
{
    using var scope = app.Services.CreateScope();
    var blobService = scope.ServiceProvider.GetRequiredService<BlobService>();
    var allowedOrigins = new[] 
    { 
        "https://flyerbox.sourceiotech.com",
        "http://localhost:5173",
        "http://localhost:3000",
        "*" // Allow all origins - remove this in production for better security
    };
    await blobService.ConfigureCorsAsync(allowedOrigins);
    app.Logger.LogInformation("Azure Blob Storage CORS configured successfully");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Failed to configure Azure Blob Storage CORS. You may need to configure it manually via Azure Portal.");
}

// Configure the HTTP request pipeline
// Enable CORS first so all responses (including errors) get CORS headers
app.UseCors("AllowReactApp");

// When an unhandled exception occurs, re-execute pipeline for /error so the 500 response gets CORS headers
app.UseExceptionHandler("/error");

app.UseSwagger();
app.UseSwaggerUI();

// Serve static files from wwwroot with CORS headers
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Add CORS headers to static files (allow any origin in development)
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");
    }
});

// Comment out HTTPS redirection for local network access
// app.UseHttpsRedirection();
app.UseAuthorization();

// Error endpoint used by UseExceptionHandler so 500 responses go through CORS
app.MapGet("/error", () => Results.Json(new { message = "An error occurred processing your request." }, statusCode: 500));

app.MapControllers();

app.Run();
