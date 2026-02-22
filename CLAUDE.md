# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

> **WSL note:** `dotnet` is not on the WSL PATH. Use the Windows binary directly:
> `'/mnt/c/Program Files/dotnet/dotnet.exe'`

```bash
# Run the API in development (HTTPS on 5001, HTTP on 5000)
'/mnt/c/Program Files/dotnet/dotnet.exe' run

# Build
'/mnt/c/Program Files/dotnet/dotnet.exe' build

# EF Core migrations
'/mnt/c/Program Files/dotnet/dotnet.exe' ef migrations add <MigrationName>
'/mnt/c/Program Files/dotnet/dotnet.exe' ef database update

# Publish (usually done via Visual Studio publish profiles in Properties/PublishProfiles/)
'/mnt/c/Program Files/dotnet/dotnet.exe' publish
```

Swagger UI is available at `/swagger` when running in any environment.

## Architecture

ASP.NET Core 8 Web API (`net8.0`) deployed to Azure App Service. There is no authentication middleware (JWT/cookies) — auth is purely checked manually in controllers by querying the database and comparing BCrypt hashes. Authorization is enforced by role checks in controller logic.

### Two Main Features

**1. Flyer Management**
- Companies upload flyer images (PNG/JPG) to **Azure Blob Storage** via `BlobService`.
- `Flyer.ImagePath` stores the **blob name** (not a full URL). On read, a short-lived SAS URL is generated via `BlobService.GetReadSasUrl()`.
- Legacy flyers may have an absolute URL in `ImagePath`; `BuildPublicImageUrl()` in `FlyerController` handles both cases.
- Flyers are soft-deleted (`IsDeleted = true`); the global query filter in `AppDbContext` excludes them automatically.

**2. Review Box (WhatsApp automation)**
- When a company adds a `ReviewCustomer`, a Day 0 WhatsApp template message is sent immediately via `WhatsAppService` (Omni/alots.io API).
- `ReviewSchedulerService` (a `BackgroundService`) polls on a configurable interval and sends Day 1 and Day 3 follow-up messages based on elapsed time since `ReviewCustomer.CreatedAt`.
- `/r/{id}` is a public redirect endpoint embedded in WhatsApp message buttons — it resolves the customer's company GBP review link and performs a 302 redirect.
- Requires `Company.GbpReviewLink` to be set before customers can be added.

### Data Model

- `Company` → has many `User`s, `Flyer`s, `ReviewCustomer`s
- `User` → belongs to one `Company` (nullable for Admin role); two roles: `Admin` and `Company` (`UserRole` enum stored as string)
- `Flyer` → belongs to `Company`; `ForDate` is a date-only column; soft-deleted via `IsDeleted`
- `ReviewCustomer` → tracks which of the 3 WhatsApp messages have been sent (`Day0Sent`, `Day1Sent`, `Day3Sent`); soft-deleted via `IsActive`

Global query filters are applied to `User` (`IsActive`), `Company` (`IsActive`), `Flyer` (`!IsDeleted`), and `ReviewCustomer` (`IsActive`). Use `.IgnoreQueryFilters()` when you need to access inactive/deleted records.

### Key Files

- `Program.cs` — Service registration, middleware pipeline, CORS, DB migration/seeding on startup
- `Data/AppDbContext.cs` — EF Core model configuration and global query filters
- `Data/DbSeeder.cs` — Seeds 3 companies + 1 admin (`admin@flyer.com` / `admin123`) + 3 company users on first run
- `Services/BlobService.cs` — Azure Blob Storage: upload, SAS URL generation, download, CORS config
- `Services/WhatsAppService.cs` / `IWhatsAppService.cs` — Omni WhatsApp API client
- `Services/ReviewMessageService.cs` — Builds template names/params for each message day
- `Services/ReviewSchedulerService.cs` — Background polling service for Day 1 / Day 3 messages

### Configuration & Secrets

Secrets (`ConnectionStrings`, `BlobStorage:ConnectionString`, `OmniWhatsApp:PhoneNumberId`, `OmniWhatsApp:ApiKey`) must **never** be committed. See `CONFIGURATION.md` for full details.

- Development: put secrets in `appsettings.Development.json` (gitignored)
- Production: use Azure App Service environment variables (double-underscore separator: `BlobStorage__ConnectionString`)
- Template: `appsettings.TEMPLATE.json`

In production, the app calls `context.Database.Migrate()` on startup. In development, it uses `EnsureCreated()`.

### JSON Serialization

Enums are serialized as camelCase strings. Property names are returned as-is (no camelCase policy on the object level — response shapes are constructed with explicit lowercase property names in controllers).
