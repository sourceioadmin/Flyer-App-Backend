# Configuration Guide

## Security: Secrets Management

This application uses sensitive configuration that should **NEVER** be committed to source control.

### Local Development Setup

1. **appsettings.Development.json** (Already configured, gitignored)
   - Contains your local database connection string and Azure Storage credentials
   - This file is automatically loaded in Development environment
   - **Never commit this file to git**

### Production Deployment Setup

#### Option 1: Azure App Service Configuration (Recommended)

Configure secrets as Environment Variables in Azure Portal:

1. Go to Azure Portal → Your App Service
2. Navigate to **Configuration** → **Application settings**
3. Add the following settings:

```
ConnectionStrings__DefaultConnection = YOUR_PRODUCTION_DATABASE_CONNECTION_STRING
BlobStorage__ConnectionString = YOUR_AZURE_STORAGE_CONNECTION_STRING
BlobStorage__ContainerName = flyers
OmniWhatsApp__PhoneNumberId = YOUR_OMNI_PHONE_NUMBER_ID
OmniWhatsApp__ApiKey = YOUR_OMNI_API_KEY
```

#### Option 2: appsettings.Production.json (Alternative)

1. Create `appsettings.Production.json` on your production server (NOT in git)
2. Use the template from `appsettings.TEMPLATE.json`
3. Fill in your production values

### Azure Storage Connection String

Get your connection string from:
1. Azure Portal → Storage Account → Access Keys
2. Copy either key1 or key2 connection string

### Database Connection String

Format for Azure SQL Database:
```
Server=tcp:your-server.database.windows.net,1433;Initial Catalog=YourDatabase;Persist Security Info=False;User ID=yourusername;Password=yourpassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

### Omni WhatsApp API (Review Box Feature)

The Review Box feature uses the Omni App (alots.io) WhatsApp Business API to send automated review request messages.

**Secrets (in `appsettings.Development.json` for dev, Azure env vars for prod):**
- `OmniWhatsApp:PhoneNumberId` - Your WhatsApp Business phone number ID from Omni App
- `OmniWhatsApp:ApiKey` - Your Omni App API key (X-API-KEY header)

**Non-secret config (in `appsettings.json`):**
- `OmniWhatsApp:BaseUrl` - API base URL (`https://alots.io/v20.0`)
- `OmniWhatsApp:LanguageCode` - Template language (`en`)
- `OmniWhatsApp:Day0TemplateName` - Template name for immediate message
- `OmniWhatsApp:Day1TemplateName` - Template name for day 1 follow-up
- `OmniWhatsApp:Day3TemplateName` - Template name for day 3 reminder

### Review Schedule Configuration

Controls the timing of automated review messages. Different values for dev vs production:

| Setting | Development | Production |
|---------|-------------|------------|
| `Message2DelayMinutes` | 2 | 1440 (24 hrs) |
| `Message3DelayMinutes` | 5 | 4320 (72 hrs) |
| `PollingIntervalSeconds` | 30 | 3600 (1 hr) |

Development values are set in `appsettings.Development.json`. Production defaults are in `appsettings.json`.

## CORS Configuration

The application automatically configures CORS for Azure Blob Storage on startup. The following origins are allowed by default:
- `https://flyerbox.sourceiotech.com` (Production)
- `http://localhost:5173` (React dev)
- `http://localhost:3000` (Alternative React dev)

To modify allowed origins, update the `allowedOrigins` array in `Program.cs`.

## Files Overview

- `appsettings.json` - Base configuration (committed to git, NO SECRETS)
- `appsettings.Development.json` - Local dev secrets (gitignored, NOT committed)
- `appsettings.Production.json` - Production secrets (gitignored, NOT committed)
- `appsettings.TEMPLATE.json` - Template for reference (committed to git)

## Security Best Practices

1. ✅ Never commit connection strings or API keys
2. ✅ Use Azure Key Vault for production secrets
3. ✅ Use Environment Variables in Azure App Service
4. ✅ Rotate keys regularly
5. ✅ Use different credentials for dev/staging/production
