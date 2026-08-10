using MedMateAI.Application.Common;
using MedMateAI.Application.Models.RecoveryPlans;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanRealtimeNotificationFactoryTests
{
    [Test]
    [Category("N")]
    public void CreateRequestNotification_ValidInputs_ReturnsRequestNotification()
    {
        // Arrange
        var request = new RecoveryPlanRequest
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DiseaseGroup = RecoveryPlanDiseaseGroup.Respiratory,
            Status = RecoveryPlanRequestStatus.WaitingForDoctor,
            RequestedAt = DateTime.UtcNow,
            Version = 1
        };
        var eventType = RecoveryPlanOutboxEventTypes.ReviewStarted;
        var queueChangeType = RecoveryPlanQueueChangeType.Added;
        var targetDoctorId = Guid.NewGuid();
        var occurredAtUtc = DateTime.UtcNow;

        // Act
        var notification = RecoveryPlanRealtimeNotificationFactory.CreateRequestNotification(
            request,
            eventType,
            queueChangeType,
            targetDoctorId,
            occurredAtUtc);

        // Assert
        Assert.That(notification, Is.Not.Null);
        Assert.That(notification.UserId, Is.EqualTo(request.UserId));
        Assert.That(notification.TargetDoctorId, Is.EqualTo(targetDoctorId));
        Assert.That(notification.RequestId, Is.EqualTo(request.Id));
        Assert.That(notification.EventType, Is.EqualTo(eventType));
        Assert.That(notification.QueueChangeType, Is.EqualTo(queueChangeType));
        Assert.That(notification.DiseaseGroup, Is.EqualTo(request.DiseaseGroup));
        Assert.That(notification.Status, Is.EqualTo(request.Status));
        Assert.That(notification.RequestedAt, Is.EqualTo(request.RequestedAt));
        Assert.That(notification.OccurredAtUtc, Is.EqualTo(occurredAtUtc));
        Assert.That(notification.Version, Is.EqualTo(request.Version));
    }

    [Test]
    [Category("N")]
    public void CreatePlanNotification_ValidInputs_ReturnsPlanNotification()
    {
        // Arrange
        var plan = new RecoveryPlan
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            RecoveryPlanRequestId = Guid.NewGuid(),
            Status = RecoveryPlanStatus.Active,
            PublishedAt = DateTime.UtcNow.AddMinutes(-5),
            ActivatedAt = DateTime.UtcNow,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
        };
        var eventType = "PlanActivated";
        var occurredAtUtc = DateTime.UtcNow;

        // Act
        var notification = RecoveryPlanRealtimeNotificationFactory.CreatePlanNotification(
            plan,
            eventType,
            occurredAtUtc);

        // Assert
        Assert.That(notification, Is.Not.Null);
        Assert.That(notification.UserId, Is.EqualTo(plan.UserId));
        Assert.That(notification.TargetDoctorId, Is.EqualTo(plan.DoctorId));
        Assert.That(notification.PlanId, Is.EqualTo(plan.Id));
        Assert.That(notification.RequestId, Is.EqualTo(plan.RecoveryPlanRequestId.Value));
        Assert.That(notification.EventType, Is.EqualTo(eventType));
        Assert.That(notification.Status, Is.EqualTo(plan.Status));
        Assert.That(notification.OccurredAtUtc, Is.EqualTo(occurredAtUtc));
        Assert.That(notification.PublishedAt, Is.EqualTo(plan.PublishedAt));
        Assert.That(notification.ActivatedAt, Is.EqualTo(plan.ActivatedAt));
        Assert.That(notification.StartDate, Is.EqualTo(plan.StartDate));
        Assert.That(notification.EndDate, Is.EqualTo(plan.EndDate));
    }

    [Test]
    [Category("A")]
    public void CreatePlanNotification_NullRecoveryPlanRequestId_ThrowsInvalidOperationException()
    {
        // Arrange
        var plan = new RecoveryPlan
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            RecoveryPlanRequestId = null,
            Status = RecoveryPlanStatus.Active
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            RecoveryPlanRealtimeNotificationFactory.CreatePlanNotification(plan, "EventType", DateTime.UtcNow));
    }

    [Test]
    [Category("N")]
    public void ResolveQueueChange_RuleNone_ReturnsNone()
    {
        // Arrange
        var descriptor = new RecoveryPlanRealtimeTransitionDescriptor("TestEvent", RecoveryPlanQueueChangeRule.None);

        // Act
        var result = descriptor.ResolveQueueChange(RecoveryPlanRequestStatus.WaitingForDoctor);

        // Assert
        Assert.That(result, Is.EqualTo(RecoveryPlanQueueChangeType.None));
    }

    [Test]
    [Category("N")]
    public void ResolveQueueChange_RuleAdded_ReturnsAdded()
    {
        // Arrange
        var descriptor = new RecoveryPlanRealtimeTransitionDescriptor("TestEvent", RecoveryPlanQueueChangeRule.Added);

        // Act
        var result = descriptor.ResolveQueueChange(RecoveryPlanRequestStatus.WaitingForDoctor);

        // Assert
        Assert.That(result, Is.EqualTo(RecoveryPlanQueueChangeType.Added));
    }

    [Test]
    [Category("N")]
    public void ResolveQueueChange_RuleRemovedWaitingForDoctor_ReturnsRemoved()
    {
        // Arrange
        var descriptor = new RecoveryPlanRealtimeTransitionDescriptor("TestEvent", RecoveryPlanQueueChangeRule.RemovedWhenPreviouslyWaiting);

        // Act
        var result = descriptor.ResolveQueueChange(RecoveryPlanRequestStatus.WaitingForDoctor);

        // Assert
        Assert.That(result, Is.EqualTo(RecoveryPlanQueueChangeType.Removed));
    }

    [Test]
    [Category("N")]
    public void ResolveQueueChange_RuleRemovedNotWaiting_ReturnsNone()
    {
        // Arrange
        var descriptor = new RecoveryPlanRealtimeTransitionDescriptor("TestEvent", RecoveryPlanQueueChangeRule.RemovedWhenPreviouslyWaiting);

        // Act
        var result = descriptor.ResolveQueueChange(RecoveryPlanRequestStatus.InReview);

        // Assert
        Assert.That(result, Is.EqualTo(RecoveryPlanQueueChangeType.None));
    }
}
