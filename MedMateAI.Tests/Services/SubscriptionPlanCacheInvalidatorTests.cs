using MedMateAI.Application.Common;
using MedMateAI.Application.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class SubscriptionPlanCacheInvalidatorTests
{
    private Mock<IDistributedCache> _cacheMock = null!;
    private Mock<ILogger<SubscriptionPlanCacheInvalidator>> _loggerMock = null!;
    private SubscriptionPlanCacheInvalidator _invalidator = null!;

    private readonly Guid _planId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<SubscriptionPlanCacheInvalidator>>();
        _invalidator = new SubscriptionPlanCacheInvalidator(_cacheMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task InvalidateAsync_HappyPath_RemovesAllThreeCacheKeys()
    {
        await _invalidator.InvalidateAsync(_planId, CancellationToken.None);

        _cacheMock.Verify(cache => cache.RemoveAsync(SubscriptionPlanCacheKeys.All, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(cache => cache.RemoveAsync(SubscriptionPlanCacheKeys.Active, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(cache => cache.RemoveAsync(SubscriptionPlanCacheKeys.ForPlan(_planId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task InvalidateAsync_ExceptionOnOneKey_StillRemovesRemainingKeys()
    {
        _cacheMock.Setup(cache => cache.RemoveAsync(SubscriptionPlanCacheKeys.All, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _invalidator.InvalidateAsync(_planId, CancellationToken.None);

        _cacheMock.Verify(cache => cache.RemoveAsync(SubscriptionPlanCacheKeys.Active, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(cache => cache.RemoveAsync(SubscriptionPlanCacheKeys.ForPlan(_planId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void InvalidateAsync_CancellationRequested_RethrowsOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        _cacheMock.Setup(cache => cache.RemoveAsync(SubscriptionPlanCacheKeys.All, cancellationSource.Token))
            .ThrowsAsync(new OperationCanceledException());

        Assert.ThrowsAsync<OperationCanceledException>(
            () => _invalidator.InvalidateAsync(_planId, cancellationSource.Token));
    }
}
