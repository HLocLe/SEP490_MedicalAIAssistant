using System.Security.Claims;
using System.Text.Json;
using MedMateAI.Application.DTOs.FeedbackReviews.Requests;
using MedMateAI.Application.DTOs.FeedbackReviews.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class FeedbackReviewServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IFeedbackReviewRepository> _feedbackRepoMock = null!;
    private Mock<IMedicalFacilityRepository> _facilityRepoMock = null!;
    private Mock<IDistributedCache> _cacheMock = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
    private Mock<IUserService> _userServiceMock = null!;
    private FeedbackReviewService _service = null!;
    private readonly Guid _userId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _feedbackRepoMock = new Mock<IFeedbackReviewRepository>();
        _facilityRepoMock = new Mock<IMedicalFacilityRepository>();
        _cacheMock = new Mock<IDistributedCache>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _userServiceMock = new Mock<IUserService>();

        _unitOfWorkMock.Setup(u => u.FeedbackReviews).Returns(_feedbackRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.MedicalFacilities).Returns(_facilityRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        SetAuthenticatedUser(_userId);

        _service = new FeedbackReviewService(
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            _httpContextAccessorMock.Object,
            _userServiceMock.Object);
    }

    private void SetAuthenticatedUser(Guid? userId)
    {
        if (userId is null)
        {
            _httpContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);
            return;
        }

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "TestAuth");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);
    }

    private void SetupCacheGetString(string key, string? value)
    {
        _cacheMock.Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value is null ? null : System.Text.Encoding.UTF8.GetBytes(value));
    }

    private static FeedbackReview MakeReview(Guid? id = null, Guid? userId = null, Guid? facilityId = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        UserId = userId ?? Guid.NewGuid(),
        FacilityId = facilityId ?? Guid.NewGuid(),
        Rating = 4,
        Status = "Approved",
        ImageUrls = new Dictionary<string, string>(),
        Facility = new MedicalFacility { FacilityName = "Facility A", Address = "Addr" },
    };

    // â”€â”€ ListFeedbackReviewsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task ListFeedbackReviewsAsync_ValidRequest_ReturnsPagedResponse()
    {
        var review = MakeReview();
        var pagedResult = new PagedResult<FeedbackReview>
        {
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPages = 1,
            Items = new List<FeedbackReview> { review },
        };

        _feedbackRepoMock.Setup(r => r.GetPagedWithDetailsAsync(1, 10, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _service.ListFeedbackReviewsAsync(1, 10);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].FacilityName, Is.EqualTo("Facility A"));
    }

    // â”€â”€ ListApprovedFacilityReviewsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task ListApprovedFacilityReviewsAsync_ValidRequest_ReturnsPagedResponse()
    {
        var facilityId = Guid.NewGuid();
        var review = MakeReview(facilityId: facilityId);
        var pagedResult = new PagedResult<FeedbackReview>
        {
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPages = 1,
            Items = new List<FeedbackReview> { review },
        };

        _feedbackRepoMock.Setup(r => r.GetApprovedByFacilityAsync(facilityId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _service.ListApprovedFacilityReviewsAsync(facilityId, 1, 10);

        Assert.That(result.Items, Has.Count.EqualTo(1));
    }

    // â”€â”€ GetFeedbackReviewByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetFeedbackReviewByIdAsync_EmptyId_ReturnsNull()
    {
        Assert.That(await _service.GetFeedbackReviewByIdAsync(Guid.Empty), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetFeedbackReviewByIdAsync_CacheHit_ReturnsCachedResponse()
    {
        var id = Guid.NewGuid();
        var cached = new FeedbackReviewResponse { Id = id, FacilityName = "Cached Facility" };
        SetupCacheGetString($"feedback-reviews:{id}", JsonSerializer.Serialize(cached));

        var result = await _service.GetFeedbackReviewByIdAsync(id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.FacilityName, Is.EqualTo("Cached Facility"));
        _feedbackRepoMock.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("A")]
    public async Task GetFeedbackReviewByIdAsync_CacheMissNotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        SetupCacheGetString($"feedback-reviews:{id}", null);
        _feedbackRepoMock.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeedbackReview?)null);

        Assert.That(await _service.GetFeedbackReviewByIdAsync(id), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetFeedbackReviewByIdAsync_CacheMissFound_QueriesDbAndSetsCache()
    {
        var id = Guid.NewGuid();
        SetupCacheGetString($"feedback-reviews:{id}", null);
        _feedbackRepoMock.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeReview(id));

        var result = await _service.GetFeedbackReviewByIdAsync(id);

        Assert.That(result, Is.Not.Null);
        _cacheMock.Verify(c => c.SetAsync(
            $"feedback-reviews:{id}",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ CreateFeedbackReviewAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("A")]
    public async Task CreateFeedbackReviewAsync_NullRequest_ReturnsError()
    {
        var (succeeded, errors, data) = await _service.CreateFeedbackReviewAsync(null!);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Request body is required."));
    }

    [Test]
    [Category("A")]
    public async Task CreateFeedbackReviewAsync_InvalidInputs_ReturnsErrors()
    {
        var request = new CreateFeedbackReviewRequest
        {
            FacilityId = Guid.Empty,
            Rating = 0,
            Comment = new string('x', 1001),
        };

        var (succeeded, errors, data) = await _service.CreateFeedbackReviewAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("FacilityId is required."));
        Assert.That(errors, Contains.Item("Rating must be between 1 and 5."));
        Assert.That(errors, Contains.Item("Comment must be less than or equal to 1000 characters."));
    }

    [Test]
    [Category("A")]
    public async Task CreateFeedbackReviewAsync_Unauthenticated_ReturnsError()
    {
        SetAuthenticatedUser(null);

        var request = new CreateFeedbackReviewRequest { FacilityId = Guid.NewGuid(), Rating = 5 };

        var (succeeded, errors, data) = await _service.CreateFeedbackReviewAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("User is not authenticated."));
    }

    [Test]
    [Category("A")]
    public async Task CreateFeedbackReviewAsync_FacilityNotFound_ReturnsError()
    {
        var request = new CreateFeedbackReviewRequest { FacilityId = Guid.NewGuid(), Rating = 5 };

        _facilityRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<MedicalFacility, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalFacility?)null);

        var (succeeded, errors, data) = await _service.CreateFeedbackReviewAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Medical facility not found."));
    }

    [Test]
    [Category("A")]
    public async Task CreateFeedbackReviewAsync_FacilityInactive_ReturnsError()
    {
        var request = new CreateFeedbackReviewRequest { FacilityId = Guid.NewGuid(), Rating = 5 };

        _facilityRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<MedicalFacility, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalFacility { IsActive = false });

        var (succeeded, errors, data) = await _service.CreateFeedbackReviewAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Medical facility is not active."));
    }

    [Test]
    [Category("A")]
    public async Task CreateFeedbackReviewAsync_AlreadyReviewed_ReturnsError()
    {
        var facilityId = Guid.NewGuid();
        var request = new CreateFeedbackReviewRequest { FacilityId = facilityId, Rating = 5 };

        _facilityRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<MedicalFacility, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalFacility { IsActive = true });

        _feedbackRepoMock.Setup(r => r.GetByUserAndFacilityAsync(_userId, facilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeReview(userId: _userId, facilityId: facilityId));

        var (succeeded, errors, data) = await _service.CreateFeedbackReviewAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("You have already reviewed this facility."));
    }

    [Test]
    [Category("N")]
    public async Task CreateFeedbackReviewAsync_ValidRequest_CreatesAndReturnsResponse()
    {
        var facilityId = Guid.NewGuid();
        var request = new CreateFeedbackReviewRequest
        {
            FacilityId = facilityId,
            Rating = 5,
            Comment = "Great!",
            ImageUrls = new Dictionary<string, string> { { "main", "https://example.com/pic.jpg" } },
        };

        _facilityRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<MedicalFacility, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalFacility { IsActive = true });

        _feedbackRepoMock.Setup(r => r.GetByUserAndFacilityAsync(_userId, facilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeedbackReview?)null);

        FeedbackReview? captured = null;
        _feedbackRepoMock.Setup(r => r.Add(It.IsAny<FeedbackReview>()))
            .Callback<FeedbackReview>(r => captured = r);

        _feedbackRepoMock.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => captured is null ? null : MakeReview(captured.Id, captured.UserId, captured.FacilityId));

        var (succeeded, errors, data) = await _service.CreateFeedbackReviewAsync(request);

        Assert.That(succeeded, Is.True);
        Assert.That(data, Is.Not.Null);
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.UserId, Is.EqualTo(_userId));
        _feedbackRepoMock.Verify(r => r.Add(It.IsAny<FeedbackReview>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ UpdateFeedbackReviewAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task UpdateFeedbackReviewAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateFeedbackReviewAsync(
            Guid.Empty, new UpdateFeedbackReviewRequest());

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Invalid feedback review id."));
    }

    [Test]
    [Category("A")]
    public async Task UpdateFeedbackReviewAsync_NullRequest_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateFeedbackReviewAsync(Guid.NewGuid(), null!);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Request body is required."));
    }

    [Test]
    [Category("A")]
    public async Task UpdateFeedbackReviewAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _feedbackRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeedbackReview?)null);

        var (succeeded, notFound, errors, data) = await _service.UpdateFeedbackReviewAsync(
            id, new UpdateFeedbackReviewRequest { Rating = 3 });

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("B")]
    public async Task UpdateFeedbackReviewAsync_InvalidRating_ReturnsError()
    {
        var id = Guid.NewGuid();
        _feedbackRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeReview(id));

        var (succeeded, notFound, errors, data) = await _service.UpdateFeedbackReviewAsync(
            id, new UpdateFeedbackReviewRequest { Rating = 6 });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Rating must be between 1 and 5."));
    }

    [Test]
    [Category("B")]
    public async Task UpdateFeedbackReviewAsync_TooManyImages_ReturnsError()
    {
        var id = Guid.NewGuid();
        var existing = MakeReview(id);
        existing.ImageUrls = new Dictionary<string, string>
        {
            { "1", "https://a.com/1.jpg" },
            { "2", "https://a.com/2.jpg" },
            { "3", "https://a.com/3.jpg" },
            { "4", "https://a.com/4.jpg" },
            { "5", "https://a.com/5.jpg" },
        };
        _feedbackRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new UpdateFeedbackReviewRequest
        {
            ImageUrls = new Dictionary<string, string?> { { "6", "https://a.com/6.jpg" } },
        };

        var (succeeded, notFound, errors, data) = await _service.UpdateFeedbackReviewAsync(id, request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("ImageUrls cannot contain more than 5 images."));
    }

    [Test]
    [Category("B")]
    public async Task UpdateFeedbackReviewAsync_ImageUrlPatchNullRemovesKey_UpdatesSuccessfully()
    {
        var id = Guid.NewGuid();
        var existing = MakeReview(id);
        existing.ImageUrls = new Dictionary<string, string> { { "main", "https://a.com/1.jpg" } };
        _feedbackRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _feedbackRepoMock.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new UpdateFeedbackReviewRequest
        {
            ImageUrls = new Dictionary<string, string?> { { "main", null } },
        };

        var (succeeded, notFound, errors, data) = await _service.UpdateFeedbackReviewAsync(id, request);

        Assert.That(succeeded, Is.True);
        Assert.That(existing.ImageUrls, Does.Not.ContainKey("main"));
        _feedbackRepoMock.Verify(r => r.Update(existing), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task UpdateFeedbackReviewAsync_ValidRequest_UpdatesAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var existing = MakeReview(id);
        _feedbackRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _feedbackRepoMock.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new UpdateFeedbackReviewRequest { Rating = 2, Comment = "Updated comment" };

        var (succeeded, notFound, errors, data) = await _service.UpdateFeedbackReviewAsync(id, request);

        Assert.That(succeeded, Is.True);
        Assert.That(existing.Rating, Is.EqualTo(2));
        Assert.That(existing.Comment, Is.EqualTo("Updated comment"));
        _cacheMock.Verify(c => c.RemoveAsync($"feedback-reviews:{id}", It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ UpdateFeedbackReviewStatusAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task UpdateFeedbackReviewStatusAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateFeedbackReviewStatusAsync(
            Guid.Empty, new UpdateFeedbackReviewStatusRequest { Status = "Hidden" });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Invalid feedback review id."));
    }

    [Test]
    [Category("A")]
    public async Task UpdateFeedbackReviewStatusAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _feedbackRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeedbackReview?)null);

        var (succeeded, notFound, errors, data) = await _service.UpdateFeedbackReviewStatusAsync(
            id, new UpdateFeedbackReviewStatusRequest { Status = "Hidden" });

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task UpdateFeedbackReviewStatusAsync_InvalidStatus_ReturnsError()
    {
        var id = Guid.NewGuid();
        _feedbackRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeReview(id));

        var (succeeded, notFound, errors, data) = await _service.UpdateFeedbackReviewStatusAsync(
            id, new UpdateFeedbackReviewStatusRequest { Status = "Unknown" });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Status is invalid."));
    }

    [Test]
    [Category("N")]
    public async Task UpdateFeedbackReviewStatusAsync_ValidStatus_UpdatesAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var existing = MakeReview(id);
        _feedbackRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _feedbackRepoMock.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var (succeeded, notFound, errors, data) = await _service.UpdateFeedbackReviewStatusAsync(
            id, new UpdateFeedbackReviewStatusRequest { Status = "hidden" });

        Assert.That(succeeded, Is.True);
        Assert.That(existing.Status, Is.EqualTo("Hidden"));
        _cacheMock.Verify(c => c.RemoveAsync($"feedback-reviews:{id}", It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ SoftDeleteFeedbackReviewAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task SoftDeleteFeedbackReviewAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors) = await _service.SoftDeleteFeedbackReviewAsync(Guid.Empty);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Invalid feedback review id."));
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteFeedbackReviewAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _feedbackRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeedbackReview?)null);

        var (succeeded, notFound, errors) = await _service.SoftDeleteFeedbackReviewAsync(id);

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("N")]
    public async Task SoftDeleteFeedbackReviewAsync_ValidId_SoftDeletesAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var existing = MakeReview(id);
        _feedbackRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var (succeeded, notFound, errors) = await _service.SoftDeleteFeedbackReviewAsync(id);

        Assert.That(succeeded, Is.True);
        Assert.That(existing.IsDeleted, Is.True);
        Assert.That(existing.DeletedAt, Is.Not.Null);
        _cacheMock.Verify(c => c.RemoveAsync($"feedback-reviews:{id}", It.IsAny<CancellationToken>()), Times.Once);
    }
}
