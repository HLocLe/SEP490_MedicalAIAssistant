using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Notifications;
using MedMateAI.Application.Options;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Application.Service;

public sealed class SaleCampaignNotificationScheduler :
    ISaleCampaignNotificationScheduler
{
    private readonly ISaleCampaignAnnouncementRepository _announcementRepository;
    private readonly ISaleCampaignAnnouncementContextService _contextService;
    private readonly ISaleCampaignNotificationContentBuilder _contentBuilder;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserPushDeviceRepository _pushDeviceRepository;
    private readonly SaleCampaignNotificationOptions _options;
    private readonly ILogger<SaleCampaignNotificationScheduler> _logger;

    public SaleCampaignNotificationScheduler(
        ISaleCampaignAnnouncementRepository announcementRepository,
        ISaleCampaignAnnouncementContextService contextService,
        ISaleCampaignNotificationContentBuilder contentBuilder,
        INotificationRepository notificationRepository,
        IUserPushDeviceRepository pushDeviceRepository,
        IOptions<SaleCampaignNotificationOptions> options,
        ILogger<SaleCampaignNotificationScheduler> logger)
    {
        _announcementRepository = announcementRepository;
        _contextService = contextService;
        _contentBuilder = contentBuilder;
        _notificationRepository = notificationRepository;
        _pushDeviceRepository = pushDeviceRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ScheduleAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        utcNow = AsUtc(utcNow);
        var campaignPageNumber = 1;

        while (!cancellationToken.IsCancellationRequested)
        {
            var campaigns = await _announcementRepository
                .GetAnnounceableCampaignPageAsync(
                    utcNow,
                    campaignPageNumber,
                    _options.CampaignBatchSize,
                    cancellationToken);
            if (campaigns.Count == 0)
            {
                break;
            }

            await ScheduleCampaignPageAsync(
                campaigns.Select(campaign => campaign.CampaignId).ToArray(),
                utcNow,
                cancellationToken);

            if (campaigns.Count < _options.CampaignBatchSize)
            {
                break;
            }

            campaignPageNumber++;
        }
    }

    private async Task ScheduleCampaignPageAsync(
        IReadOnlyCollection<Guid> campaignIds,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var userPageNumber = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            var recipients = await _announcementRepository
                .GetPatientRecipientPageAsync(
                    userPageNumber,
                    _options.UserBatchSize,
                    cancellationToken);
            if (recipients.Count == 0)
            {
                break;
            }

            var devicesByUser = await _pushDeviceRepository
                .GetActiveByUserIdsAsync(
                    recipients.Select(recipient => recipient.UserId).ToArray(),
                    cancellationToken);

            foreach (var recipient in recipients)
            {
                try
                {
                    devicesByUser.TryGetValue(recipient.UserId, out var devices);
                    devices ??= Array.Empty<UserPushDeviceData>();
                    if (string.IsNullOrWhiteSpace(recipient.Email)
                        && devices.Count == 0)
                    {
                        continue;
                    }

                    var contexts = await _contextService.GetEligibleContextsAsync(
                        recipient,
                        campaignIds,
                        utcNow,
                        cancellationToken);
                    foreach (var context in contexts)
                    {
                        await EnqueueAsync(
                            context,
                            devices,
                            utcNow,
                            cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        "Sale campaign announcements for user {UserId} failed with {FailureType}.",
                        recipient.UserId,
                        exception.GetType().Name);
                }
            }

            if (recipients.Count < _options.UserBatchSize)
            {
                break;
            }

            userPageNumber++;
        }
    }

    private async Task EnqueueAsync(
        SaleCampaignAnnouncementContext context,
        IReadOnlyList<UserPushDeviceData> devices,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(context.Email))
        {
            var emailContent = _contentBuilder.Build(
                context,
                NotificationChannels.Email);
            await TryInsertAsync(
                CreateNotification(
                    context,
                    emailContent,
                    NotificationChannels.Email,
                    pushDeviceId: null,
                    BuildEmailDedupeKey(context),
                    utcNow),
                context,
                pushDeviceId: null,
                cancellationToken);
        }

        var pushContent = _contentBuilder.Build(
            context,
            NotificationChannels.Push);
        foreach (var device in devices)
        {
            await TryInsertAsync(
                CreateNotification(
                    context,
                    pushContent,
                    NotificationChannels.Push,
                    device.Id,
                    BuildPushDedupeKey(context, device.Id),
                    utcNow),
                context,
                device.Id,
                cancellationToken);
        }
    }

    private static Notification CreateNotification(
        SaleCampaignAnnouncementContext context,
        SaleCampaignNotificationContent content,
        string channel,
        Guid? pushDeviceId,
        string dedupeKey,
        DateTime utcNow)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = context.UserId,
            PushDeviceId = pushDeviceId,
            Title = content.Title,
            Message = content.Body,
            Channel = channel,
            Status = NotificationStatuses.Pending,
            NotificationType = NotificationTypes.SaleCampaignAnnouncement,
            ReferenceType = NotificationReferenceTypes.SaleCampaign,
            ReferenceId = context.CampaignId,
            ScheduledAt = null,
            DedupeKey = dedupeKey,
            CreatedAt = utcNow
        };
    }

    private async Task TryInsertAsync(
        Notification notification,
        SaleCampaignAnnouncementContext context,
        Guid? pushDeviceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationRepository.TryInsertAsync(
                notification,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Sale announcement enqueue failed for campaign {CampaignId}, user {UserId}, device {PushDeviceId} with {FailureType}.",
                context.CampaignId,
                context.UserId,
                pushDeviceId,
                exception.GetType().Name);
        }
    }

    private static string BuildEmailDedupeKey(
        SaleCampaignAnnouncementContext context)
    {
        return $"sale-announcement:{context.CampaignId:N}:{context.UserId:N}:email";
    }

    private static string BuildPushDedupeKey(
        SaleCampaignAnnouncementContext context,
        Guid pushDeviceId)
    {
        return $"sale-announcement:{context.CampaignId:N}:{context.UserId:N}:push:{pushDeviceId:N}";
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };
    }
}
