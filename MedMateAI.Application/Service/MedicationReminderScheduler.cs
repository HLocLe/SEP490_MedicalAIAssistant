using System.Globalization;
using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Application.Service;

public sealed class MedicationReminderScheduler : IMedicationReminderScheduler
{
    private const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";
    private const int MaximumTimeZoneOffsetHours = 14;

    private readonly IUserMedicationRepository _medicationRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly RecoveryPlanJobOptions _options;
    private readonly ILogger<MedicationReminderScheduler> _logger;

    public MedicationReminderScheduler(
        IUserMedicationRepository medicationRepository,
        INotificationRepository notificationRepository,
        IOptions<RecoveryPlanJobOptions> options,
        ILogger<MedicationReminderScheduler> logger)
    {
        _medicationRepository = medicationRepository;
        _notificationRepository = notificationRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ScheduleAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        utcNow = AsUtc(utcNow);
        var windowStartUtc = utcNow.AddMinutes(
            -_options.MedicationScheduleLookbackMinutes);
        var windowEndUtc = utcNow.AddHours(
            _options.MedicationScheduleHorizonHours);
        var earliestLocalDate = DateOnly.FromDateTime(
            windowStartUtc.AddHours(-MaximumTimeZoneOffsetHours));
        var latestLocalDate = DateOnly.FromDateTime(
            windowEndUtc.AddHours(MaximumTimeZoneOffsetHours));
        var pageNumber = 1;

        while (!cancellationToken.IsCancellationRequested)
        {
            var schedules = await _medicationRepository.GetActiveSchedulesAsync(
                earliestLocalDate,
                latestLocalDate,
                pageNumber,
                _options.MedicationSchedulerBatchSize,
                cancellationToken);

            foreach (var schedule in schedules)
            {
                try
                {
                    await ScheduleOccurrencesAsync(
                        schedule,
                        windowStartUtc,
                        windowEndUtc,
                        utcNow,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        "Medication reminder schedule {ReminderTimeId} failed with {FailureType}.",
                        schedule.ReminderTimeId,
                        exception.GetType().Name);
                }
            }

            if (schedules.Count < _options.MedicationSchedulerBatchSize)
            {
                break;
            }

            pageNumber++;
        }
    }

    private async Task ScheduleOccurrencesAsync(
        MedicationReminderScheduleData schedule,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var timeZone = ResolveTimeZone(schedule);
        var localWindowStart = TimeZoneInfo.ConvertTimeFromUtc(
            windowStartUtc,
            timeZone);
        var localWindowEnd = TimeZoneInfo.ConvertTimeFromUtc(
            windowEndUtc,
            timeZone);
        var firstLocalDate = DateOnly.FromDateTime(localWindowStart);
        var lastLocalDate = DateOnly.FromDateTime(localWindowEnd);

        for (var localDate = firstLocalDate;
             localDate <= lastLocalDate;
             localDate = localDate.AddDays(1))
        {
            if (localDate < schedule.StartDate || localDate > schedule.EndDate)
            {
                continue;
            }

            var dueUtc = ConvertOccurrenceToUtc(
                schedule,
                localDate,
                timeZone);
            if (!dueUtc.HasValue
                || dueUtc.Value < windowStartUtc
                || dueUtc.Value > windowEndUtc)
            {
                continue;
            }

            await _notificationRepository.TryInsertAsync(
                CreateNotification(schedule, localDate, dueUtc.Value, utcNow),
                cancellationToken);
        }
    }

    private DateTime? ConvertOccurrenceToUtc(
        MedicationReminderScheduleData schedule,
        DateOnly localDate,
        TimeZoneInfo timeZone)
    {
        var localDateTime = DateTime.SpecifyKind(
            localDate.ToDateTime(schedule.TimeOfDay),
            DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(localDateTime))
        {
            _logger.LogWarning(
                "Medication reminder schedule {ReminderTimeId} falls in an invalid local time and was skipped.",
                schedule.ReminderTimeId);
            return null;
        }

        if (!timeZone.IsAmbiguousTime(localDateTime))
        {
            return TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
        }

        // During a repeated DST hour, choose the earlier UTC instant deterministically.
        return timeZone.GetAmbiguousTimeOffsets(localDateTime)
            .Select(offset => new DateTimeOffset(localDateTime, offset).UtcDateTime)
            .Min();
    }

    private TimeZoneInfo ResolveTimeZone(MedicationReminderScheduleData schedule)
    {
        if (!string.IsNullOrWhiteSpace(schedule.TimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                LogTimeZoneFallback(schedule.ReminderTimeId);
            }
            catch (InvalidTimeZoneException)
            {
                LogTimeZoneFallback(schedule.ReminderTimeId);
            }
        }
        else
        {
            LogTimeZoneFallback(schedule.ReminderTimeId);
        }

        return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
    }

    private void LogTimeZoneFallback(Guid reminderTimeId)
    {
        _logger.LogWarning(
            "Medication reminder schedule {ReminderTimeId} has no valid timezone; using the default timezone.",
            reminderTimeId);
    }

    private static Notification CreateNotification(
        MedicationReminderScheduleData schedule,
        DateOnly localDate,
        DateTime dueUtc,
        DateTime utcNow)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = schedule.UserId,
            ReminderId = null,
            Title = MedicationReminderNotificationContent.Title,
            Message = MedicationReminderNotificationContent.Message,
            Channel = NotificationChannels.Email,
            Status = NotificationStatuses.Pending,
            SentAt = null,
            NotificationType = NotificationTypes.MedicationReminder,
            ReferenceType = NotificationReferenceTypes.UserMedicationReminderTime,
            ReferenceId = schedule.ReminderTimeId,
            ScheduledAt = dueUtc,
            AttemptCount = 0,
            LastError = null,
            DedupeKey = BuildMedicationReminderDedupeKey(
                schedule.ReminderTimeId,
                localDate,
                schedule.TimeOfDay,
                dueUtc),
            CreatedAt = utcNow
        };
    }

    private static string BuildMedicationReminderDedupeKey(
        Guid reminderTimeId,
        DateOnly localDate,
        TimeOnly localTime,
        DateTime dueUtc)
    {
        var datePart = localDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var timePart = localTime.ToString("HHmm", CultureInfo.InvariantCulture);
        var dueUtcPart = AsUtc(dueUtc).ToString(
            "yyyyMMddHHmm",
            CultureInfo.InvariantCulture);

        return $"medication-reminder:{reminderTimeId:N}:{datePart}:{timePart}:{dueUtcPart}";
    }

    private static DateTime AsUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.ToUniversalTime();
    }
}
