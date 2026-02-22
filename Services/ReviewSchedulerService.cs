using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using backend.Data;
using backend.Models;

namespace backend.Services;

/// <summary>
/// Background service that polls for pending review messages (Day 1 and Day 3)
/// and sends them via WhatsApp when their scheduled time arrives.
/// </summary>
public class ReviewSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReviewScheduleOptions _scheduleOptions;
    private readonly ILogger<ReviewSchedulerService> _logger;

    public ReviewSchedulerService(IServiceScopeFactory scopeFactory,
        IOptions<ReviewScheduleOptions> scheduleOptions,
        ILogger<ReviewSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _scheduleOptions = scheduleOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ReviewScheduler started. Polling every {Interval}s. Message2 delay: {M2}min, Message3 delay: {M3}min",
            _scheduleOptions.PollingIntervalSeconds,
            _scheduleOptions.Message2DelayMinutes,
            _scheduleOptions.Message3DelayMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("ReviewScheduler: polling cycle started");
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ReviewScheduler polling cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(_scheduleOptions.PollingIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
        var messageService = scope.ServiceProvider.GetRequiredService<ReviewMessageService>();
        var omniOptions = scope.ServiceProvider.GetRequiredService<IOptions<OmniWhatsAppOptions>>().Value;

        var now = DateTime.UtcNow;

        // Process Day 0 retries (failed to send on creation)
        await ProcessDay0RetriesAsync(context, whatsAppService, messageService, omniOptions, now, stoppingToken);

        // Process Day 1 messages
        await ProcessDay1MessagesAsync(context, whatsAppService, messageService, now, stoppingToken);

        // Process Day 3 messages
        await ProcessDay3MessagesAsync(context, whatsAppService, messageService, now, stoppingToken);
    }

    private async Task ProcessDay0RetriesAsync(AppDbContext context, IWhatsAppService whatsAppService,
        ReviewMessageService messageService, OmniWhatsAppOptions omniOptions, DateTime now, CancellationToken stoppingToken)
    {
        var pendingDay0 = await context.ReviewCustomers
            .Include(rc => rc.Company)
            .Where(rc => !rc.Day0Sent && rc.IsActive)
            .ToListAsync(stoppingToken);

        if (pendingDay0.Count > 0)
        {
            _logger.LogInformation("Found {Count} customers pending Day 0 message (will send now)",
                pendingDay0.Count);
        }
        else
        {
            _logger.LogDebug("Day 0: no pending customers (Day0Sent=0 and IsActive=1)");
        }

        foreach (var customer in pendingDay0)
        {
            if (stoppingToken.IsCancellationRequested) break;

            if (customer.Company == null || string.IsNullOrWhiteSpace(customer.Company.GbpReviewLink))
            {
                _logger.LogWarning("Skipping customer {Id} ({Name}): Company or GBP link missing", customer.Id, customer.CustomerName);
                continue;
            }

            // Optionally send "Hi" first (can trigger 131049; disable by default)
            if (omniOptions.SendHiBeforeTemplate)
            {
                await whatsAppService.SendTextMessageAsync(customer.PhoneNumber, "Hi");
                await Task.Delay(2000, stoppingToken);
            }

            var (templateName, bodyParams, buttonSuffix, headerImageLink, headerImageId, languageCode) = messageService.GetDay0Message(
                customer.Id, customer.CustomerName, customer.Company.Name, customer.Company.GbpReviewLink!);

            var sent = await whatsAppService.SendTemplateMessageAsync(
                customer.PhoneNumber, templateName, bodyParams, buttonSuffix, headerImageLink, headerImageId, languageCode);

            if (sent)
            {
                customer.Day0Sent = true;
                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Day 0 retry: message sent to customer {Id} ({Phone})",
                    customer.Id, customer.PhoneNumber);
            }
        }
    }

    private async Task ProcessDay1MessagesAsync(AppDbContext context, IWhatsAppService whatsAppService,
        ReviewMessageService messageService, DateTime now, CancellationToken stoppingToken)
    {
        var cutoff = now.AddMinutes(-_scheduleOptions.Message2DelayMinutes);

        var pendingDay1 = await context.ReviewCustomers
            .Include(rc => rc.Company)
            .Where(rc => rc.Day0Sent && !rc.Day1Sent && rc.IsActive && rc.CreatedAt <= cutoff)
            .ToListAsync(stoppingToken);

        if (pendingDay1.Count > 0)
        {
            _logger.LogInformation("Found {Count} customers pending Day 1 message (cutoff: {Cutoff})",
                pendingDay1.Count, cutoff);
        }

        foreach (var customer in pendingDay1)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var (templateName, bodyParams, buttonSuffix) = messageService.GetDay1Message(
                customer.Id, customer.CustomerName, customer.Company.Name, customer.Company.GbpReviewLink!);

            var sent = await whatsAppService.SendTemplateMessageAsync(
                customer.PhoneNumber, templateName, bodyParams, buttonSuffix);

            if (sent)
            {
                customer.Day1Sent = true;
                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Day 1 message sent to customer {Id} ({Phone})",
                    customer.Id, customer.PhoneNumber);
            }
            else
            {
                _logger.LogWarning("Failed to send Day 1 message to customer {Id} ({Phone}). Will retry next cycle.",
                    customer.Id, customer.PhoneNumber);
            }
        }
    }

    private async Task ProcessDay3MessagesAsync(AppDbContext context, IWhatsAppService whatsAppService,
        ReviewMessageService messageService, DateTime now, CancellationToken stoppingToken)
    {
        var cutoff = now.AddMinutes(-_scheduleOptions.Message3DelayMinutes);

        var pendingDay3 = await context.ReviewCustomers
            .Include(rc => rc.Company)
            .Where(rc => rc.Day1Sent && !rc.Day3Sent && rc.IsActive && rc.CreatedAt <= cutoff)
            .ToListAsync(stoppingToken);

        if (pendingDay3.Count > 0)
        {
            _logger.LogInformation("Found {Count} customers pending Day 3 message (cutoff: {Cutoff})",
                pendingDay3.Count, cutoff);
        }

        foreach (var customer in pendingDay3)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var (templateName, bodyParams, buttonSuffix) = messageService.GetDay3Message(
                customer.Id, customer.CustomerName, customer.Company.Name, customer.Company.GbpReviewLink!);

            var sent = await whatsAppService.SendTemplateMessageAsync(
                customer.PhoneNumber, templateName, bodyParams, buttonSuffix);

            if (sent)
            {
                customer.Day3Sent = true;
                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Day 3 message sent to customer {Id} ({Phone})",
                    customer.Id, customer.PhoneNumber);
            }
            else
            {
                _logger.LogWarning("Failed to send Day 3 message to customer {Id} ({Phone}). Will retry next cycle.",
                    customer.Id, customer.PhoneNumber);
            }
        }
    }
}
