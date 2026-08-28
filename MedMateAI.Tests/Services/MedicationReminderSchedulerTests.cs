using MedMateAI.Application.Common;
using MedMateAI.Application.Options;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class MedicationReminderSchedulerTests
{
    private Mock<IUserMedicationRepository> _medicationRepositoryMock = null!;
    private Mock<INotificationRepository> _notificationRepositoryMock = null!;
    private Mock<IUserPushDeviceRepository> _pushDeviceRepositoryMock = null!;
    private Mock<ILogger<MedicationReminderScheduler>> _loggerMock = null!;
    private MedicationReminderScheduler _scheduler = null!;

    private readonly DateTime _utcNow = new(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _medicationRepositoryMock = new Mock<IUserMedicationRepository>();
        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _pushDeviceRepositoryMock = new Mock<IUserPushDeviceRepository>();
        _loggerMock = new Mock<ILogger<MedicationReminderScheduler>>();

        _pushDeviceRepositoryMock
            .Setup(repository => repository.GetActiveByUserIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserPushDeviceData>());

        _scheduler = BuildScheduler(new RecoveryPlanJobOptions());
    }

    private MedicationReminderScheduler BuildScheduler(RecoveryPlanJobOptions options) =>
        new(
            _medicationRepositoryMock.Object,
            _notificationRepositoryMock.Object,
            _pushDeviceRepositoryMock.Object,
            Options.Create(options),
            _loggerMock.Object);

    [Test]
    public async Task ScheduleAsync_NoActiveSchedules_InsertsNothing()
    {
        SetupSchedulePage(1, 200, Array.Empty<MedicationReminderScheduleData>());

        await _scheduler.ScheduleAsync(_utcNow, CancellationToken.None);

        _notificationRepositoryMock.Verify(
            repository => repository.TryInsertAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ScheduleAsync_OccurrenceWithinWindow_InsertsSingleNotification()
    {
        var schedule = new MedicationReminderScheduleData(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            new TimeOnly(8, 0),
            "Asia/Ho_Chi_Minh");
        SetupSchedulePage(1, 200, new[] { schedule });

        await _scheduler.ScheduleAsync(_utcNow, CancellationToken.None);

        _notificationRepositoryMock.Verify(repository => repository.TryInsertAsync(
            It.Is<Notification>(notification =>
                notification.UserId == schedule.UserId
                && notification.ReferenceId == schedule.ReminderTimeId
                && notification.ReferenceType == NotificationReferenceTypes.UserMedicationReminderTime
                && notification.NotificationType == NotificationTypes.MedicationReminder
                && notification.Status == NotificationStatuses.Pending
                && notification.ScheduledAt == new DateTime(2026, 8, 13, 1, 0, 0, DateTimeKind.Utc)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ScheduleAsync_OccurrenceBeforeScheduleStartDate_SkipsInsert()
    {
        var schedule = new MedicationReminderScheduleData(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            new TimeOnly(8, 0),
            "Asia/Ho_Chi_Minh");
        SetupSchedulePage(1, 200, new[] { schedule });

        await _scheduler.ScheduleAsync(_utcNow, CancellationToken.None);

        _notificationRepositoryMock.Verify(
            repository => repository.TryInsertAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ScheduleAsync_OneScheduleThrows_LogsWarningAndStillProcessesRemainingSchedules()
    {
        var failingSchedule = new MedicationReminderScheduleData(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            new TimeOnly(8, 0),
            "Asia/Ho_Chi_Minh");
        var healthySchedule = new MedicationReminderScheduleData(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            new TimeOnly(9, 0),
            "Asia/Ho_Chi_Minh");
        SetupSchedulePage(1, 200, new[] { failingSchedule, healthySchedule });
        _notificationRepositoryMock.Setup(repository => repository.TryInsertAsync(
                It.Is<Notification>(n => n.ReferenceId == failingSchedule.ReminderTimeId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("insert failure"));
        _notificationRepositoryMock.Setup(repository => repository.TryInsertAsync(
                It.Is<Notification>(n => n.ReferenceId == healthySchedule.ReminderTimeId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _scheduler.ScheduleAsync(_utcNow, CancellationToken.None);

        _notificationRepositoryMock.Verify(repository => repository.TryInsertAsync(
            It.Is<Notification>(n => n.ReferenceId == healthySchedule.ReminderTimeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void ScheduleAsync_CancelledDuringProcessing_PropagatesOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        var schedule = new MedicationReminderScheduleData(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            new TimeOnly(8, 0),
            "Asia/Ho_Chi_Minh");
        SetupSchedulePage(1, 200, new[] { schedule });
        _notificationRepositoryMock.Setup(repository => repository.TryInsertAsync(
                It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cancellationSource.Cancel();
                throw new OperationCanceledException(cancellationSource.Token);
            });

        Assert.ThrowsAsync<OperationCanceledException>(
            () => _scheduler.ScheduleAsync(_utcNow, cancellationSource.Token));
    }

    [Test]
    public async Task ScheduleAsync_MultiplePages_PaginatesUntilShortPage()
    {
        var scheduler = BuildScheduler(new RecoveryPlanJobOptions { MedicationSchedulerBatchSize = 1 });
        var firstPageSchedule = new MedicationReminderScheduleData(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            new TimeOnly(8, 0),
            "Asia/Ho_Chi_Minh");
        SetupSchedulePage(1, 1, new[] { firstPageSchedule });
        SetupSchedulePage(2, 1, Array.Empty<MedicationReminderScheduleData>());

        await scheduler.ScheduleAsync(_utcNow, CancellationToken.None);

        _medicationRepositoryMock.Verify(repository => repository.GetActiveSchedulesAsync(
            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), 1, 1, It.IsAny<CancellationToken>()), Times.Once);
        _medicationRepositoryMock.Verify(repository => repository.GetActiveSchedulesAsync(
            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), 2, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ScheduleAsync_InvalidTimeZoneId_FallsBackToDefaultAndLogsWarning()
    {
        var schedule = new MedicationReminderScheduleData(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            new TimeOnly(8, 0),
            "Not/AReal/Zone");
        SetupSchedulePage(1, 200, new[] { schedule });

        await _scheduler.ScheduleAsync(_utcNow, CancellationToken.None);

        _notificationRepositoryMock.Verify(repository => repository.TryInsertAsync(
            It.Is<Notification>(n => n.ScheduledAt == new DateTime(2026, 8, 13, 1, 0, 0, DateTimeKind.Utc)),
            It.IsAny<CancellationToken>()), Times.Once);
        _loggerMock.Verify(logger => logger.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, type) => state.ToString()!.Contains("no valid timezone")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Test]
    public async Task ScheduleAsync_LocalTimeFallsInDstGap_SkipsOccurrenceAndLogsWarning()
    {
        var scheduler = BuildScheduler(new RecoveryPlanJobOptions
        {
            MedicationScheduleLookbackMinutes = 5,
            MedicationScheduleHorizonHours = 2
        });
        var dstTransitionUtcNow = new DateTime(2026, 3, 8, 10, 0, 0, DateTimeKind.Utc);
        var schedule = new MedicationReminderScheduleData(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 31),
            new TimeOnly(2, 30),
            "America/New_York");
        SetupSchedulePage(1, 200, new[] { schedule });

        await scheduler.ScheduleAsync(dstTransitionUtcNow, CancellationToken.None);

        _notificationRepositoryMock.Verify(
            repository => repository.TryInsertAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _loggerMock.Verify(logger => logger.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, type) => state.ToString()!.Contains("invalid local time")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Test]
    public async Task ScheduleAsync_ActivePushDevice_CreatesEmailAndPushNotifications()
    {
        var schedule = CreateActiveSchedule();
        var device = new UserPushDeviceData(
            Guid.NewGuid(),
            schedule.UserId,
            "ExponentPushToken[test]",
            1,
            "android",
            true);
        SetupSchedulePage(1, 200, new[] { schedule });
        SetupPushDevices(schedule.UserId, new[] { device });
        var notifications = CaptureInsertedNotifications();

        await _scheduler.ScheduleAsync(_utcNow, CancellationToken.None);

        var email = notifications.Single(notification =>
            notification.Channel == NotificationChannels.Email);
        var push = notifications.Single(notification =>
            notification.Channel == NotificationChannels.Push);
        Assert.Multiple(() =>
        {
            Assert.That(notifications, Has.Count.EqualTo(2));
            Assert.That(push.UserId, Is.EqualTo(schedule.UserId));
            Assert.That(push.PushDeviceId, Is.EqualTo(device.Id));
            Assert.That(push.NotificationType, Is.EqualTo(NotificationTypes.MedicationReminder));
            Assert.That(push.ReferenceType, Is.EqualTo(NotificationReferenceTypes.UserMedicationReminderTime));
            Assert.That(push.ReferenceId, Is.EqualTo(schedule.ReminderTimeId));
            Assert.That(push.Status, Is.EqualTo(NotificationStatuses.Pending));
            Assert.That(push.ScheduledAt, Is.EqualTo(new DateTime(2026, 8, 13, 1, 0, 0, DateTimeKind.Utc)));
            Assert.That(push.DedupeKey, Is.Not.EqualTo(email.DedupeKey));
            Assert.That(push.DedupeKey, Does.EndWith($":push:{device.Id:N}"));
        });
    }

    [Test]
    public async Task ScheduleAsync_MultipleActivePushDevices_CreatesOnePushPerDevice()
    {
        var schedule = CreateActiveSchedule();
        var devices = new[]
        {
            new UserPushDeviceData(
                Guid.NewGuid(),
                schedule.UserId,
                "ExponentPushToken[first]",
                1,
                "android",
                true),
            new UserPushDeviceData(
                Guid.NewGuid(),
                schedule.UserId,
                "ExponentPushToken[second]",
                1,
                "ios",
                true)
        };
        SetupSchedulePage(1, 200, new[] { schedule });
        SetupPushDevices(schedule.UserId, devices);
        var notifications = CaptureInsertedNotifications();

        await _scheduler.ScheduleAsync(_utcNow, CancellationToken.None);

        var pushNotifications = notifications
            .Where(notification => notification.Channel == NotificationChannels.Push)
            .ToList();
        Assert.Multiple(() =>
        {
            Assert.That(notifications.Count(notification =>
                notification.Channel == NotificationChannels.Email), Is.EqualTo(1));
            Assert.That(pushNotifications, Has.Count.EqualTo(2));
            Assert.That(
                pushNotifications.Select(notification => notification.PushDeviceId),
                Is.EquivalentTo(devices.Select(device => (Guid?)device.Id)));
            Assert.That(
                pushNotifications.Select(notification => notification.DedupeKey),
                Is.Unique);
        });
    }

    [Test]
    public async Task ScheduleAsync_NoPushDevice_CreatesEmailOnly()
    {
        var schedule = CreateActiveSchedule();
        SetupSchedulePage(1, 200, new[] { schedule });
        var notifications = CaptureInsertedNotifications();

        await _scheduler.ScheduleAsync(_utcNow, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(notifications[0].Channel, Is.EqualTo(NotificationChannels.Email));
            Assert.That(notifications.Count(notification =>
                notification.Channel == NotificationChannels.Push), Is.Zero);
        });
    }

    private static MedicationReminderScheduleData CreateActiveSchedule()
    {
        return new MedicationReminderScheduleData(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            new TimeOnly(8, 0),
            "Asia/Ho_Chi_Minh");
    }

    private List<Notification> CaptureInsertedNotifications()
    {
        var notifications = new List<Notification>();
        _notificationRepositoryMock
            .Setup(repository => repository.TryInsertAsync(
                It.IsAny<Notification>(),
                It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((notification, _) =>
                notifications.Add(notification))
            .ReturnsAsync(true);
        return notifications;
    }

    private void SetupPushDevices(
        Guid userId,
        IReadOnlyList<UserPushDeviceData> devices)
    {
        _pushDeviceRepositoryMock
            .Setup(repository => repository.GetActiveByUserIdAsync(
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices);
    }

    private void SetupSchedulePage(
        int pageNumber,
        int pageSize,
        IReadOnlyList<MedicationReminderScheduleData> schedules)
    {
        _medicationRepositoryMock.Setup(repository => repository.GetActiveSchedulesAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), pageNumber, pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedules);
        _notificationRepositoryMock
            .Setup(repository => repository.TryInsertAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }
}
