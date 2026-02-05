using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace backend.Services;

public class BlobService
{
    private readonly BlobContainerClient _container;
    private readonly BlobServiceClient _serviceClient;

    public BlobService(IConfiguration config)
    {
        var connectionString = config["BlobStorage:ConnectionString"];
        var containerName = config["BlobStorage:ContainerName"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("BlobStorage:ConnectionString is not configured.");
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException("BlobStorage:ContainerName is not configured.");
        }

        _serviceClient = new BlobServiceClient(connectionString);
        _container = _serviceClient.GetBlobContainerClient(containerName);
    }

    /// <summary>
    /// Configures CORS for the blob service to allow cross-origin requests.
    /// This should be called once during application startup or setup.
    /// </summary>
    public async Task ConfigureCorsAsync(string[] allowedOrigins)
    {
        var cors = new List<BlobCorsRule>
        {
            new BlobCorsRule
            {
                AllowedOrigins = string.Join(",", allowedOrigins),
                AllowedMethods = "GET,HEAD,OPTIONS",
                AllowedHeaders = "*",
                ExposedHeaders = "*",
                MaxAgeInSeconds = 3600
            }
        };

        var properties = await _serviceClient.GetPropertiesAsync();
        properties.Value.Cors.Clear();
        foreach (var rule in cors)
        {
            properties.Value.Cors.Add(rule);
        }

        await _serviceClient.SetPropertiesAsync(properties.Value);
    }

    /// <summary>
    /// Uploads the file and returns the blob name (not a URL).
    /// </summary>
    public async Task<string> UploadAsync(IFormFile file)
    {
        var blobName = $"{Guid.NewGuid()}_{file.FileName}";
        var blobClient = _container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, overwrite: true);

        return blobName;
    }

    /// <summary>
    /// Generates a read-only SAS URL for the given blob name.
    /// </summary>
    public string GetReadSasUrl(string blobName, TimeSpan lifetime)
    {
        var blobClient = _container.GetBlobClient(blobName);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException("Blob client cannot generate SAS URI. Ensure the connection string uses an account key, not a SAS.");
        }

        var expiresOn = DateTimeOffset.UtcNow.Add(lifetime);
        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, expiresOn);

        return sasUri.ToString();
    }

    /// <summary>
    /// Downloads a blob and returns its content as a stream.
    /// </summary>
    public async Task<Stream> DownloadAsync(string blobName)
    {
        var blobClient = _container.GetBlobClient(blobName);
        
        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"Blob '{blobName}' not found in container.");
        }

        // Download to a MemoryStream to avoid stream disposal issues
        var memoryStream = new MemoryStream();
        await blobClient.DownloadToAsync(memoryStream);
        memoryStream.Position = 0; // Reset position to start
        
        return memoryStream;
    }
}

