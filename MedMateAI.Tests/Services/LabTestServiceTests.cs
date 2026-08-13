using System.Linq.Expressions;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.LabTests.Requests;
using MedMateAI.Application.DTOs.LabTests.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.ServiceCredits;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class LabTestServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IGenericRepository<LabTestSession>> _sessionsMock = null!;
    private Mock<ILabTestSessionRepository> _sessionDetailsMock = null!;
    private Mock<IGenericRepository<LabTestOcrExtract>> _ocrExtractsMock = null!;
    private Mock<ILabTestJobScheduler> _schedulerMock = null!;
    private Mock<ILabTestResultAnalyzer> _analyzerMock = null!;
    private Mock<ILabTestQuotaService> _quotaServiceMock = null!;
    private LabTestService _service = null!;
    private readonly Guid _userId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _sessionsMock = new Mock<IGenericRepository<LabTestSession>>();
        _sessionDetailsMock = new Mock<ILabTestSessionRepository>();
        _ocrExtractsMock = new Mock<IGenericRepository<LabTestOcrExtract>>();
        _schedulerMock = new Mock<ILabTestJobScheduler>();
        _analyzerMock = new Mock<ILabTestResultAnalyzer>();
        _quotaServiceMock = new Mock<ILabTestQuotaService>();

        _unitOfWorkMock.Setup(u => u.LabTestSessions).Returns(_sessionsMock.Object);
        _unitOfWorkMock.Setup(u => u.LabTestSessionDetails).Returns(_sessionDetailsMock.Object);
        _unitOfWorkMock.Setup(u => u.LabTestOcrExtracts).Returns(_ocrExtractsMock.Object);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _service = new LabTestService(
            _unitOfWorkMock.Object,
            _schedulerMock.Object,
            _analyzerMock.Object,
            _quotaServiceMock.Object);
    }

    // â”€â”€ AnalyzeFromDocumentUrlAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task AnalyzeFromDocumentUrlAsync_EmptyUserId_ReturnsError()
    {
        // Arrange
        var req = new LabTestAnalyzeRequest { DocumentUrl = "http://example.com/test.png" };

        // Act
        var result = await _service.AnalyzeFromDocumentUrlAsync(Guid.Empty, req);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("Id người dùng là bắt buộc"));
    }

    [TestCase("http://example.com/test.txt")]
    [TestCase("http://example.com/test")]
    [TestCase("   ")]
    [Category("A")]
    public async Task AnalyzeFromDocumentUrlAsync_InvalidDocumentUrl_ReturnsErrors(string url)
    {
        // Arrange
        var req = new LabTestAnalyzeRequest { DocumentUrl = url };

        // Act
        var result = await _service.AnalyzeFromDocumentUrlAsync(_userId, req);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Is.Not.Empty);
    }

    [TestCase(-1)]
    [TestCase(151)]
    [Category("B")]
    public async Task AnalyzeFromDocumentUrlAsync_InvalidPatientAge_ReturnsError(int age)
    {
        // Arrange
        var req = new LabTestAnalyzeRequest
        {
            DocumentUrl = "http://example.com/test.png",
            PatientAgeAtTest = age
        };

        // Act
        var result = await _service.AnalyzeFromDocumentUrlAsync(_userId, req);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Contains.Item("PatientAgeAtTest không hợp lệ"));
    }

    [Test]
    [Category("N")]
    public async Task AnalyzeFromDocumentUrlAsync_ValidRequest_CreatesSessionAndSchedulesJob()
    {
        // Arrange
        var req = new LabTestAnalyzeRequest
        {
            DocumentUrl = "http://example.com/test.png",
            PatientAgeAtTest = 25,
            PatientGenderAtTest = Gender.Male,
            TestDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        LabTestSession? savedSession = null;
        _sessionsMock.Setup(r => r.Add(It.IsAny<LabTestSession>()))
            .Callback<LabTestSession>(s => savedSession = s);
        _quotaServiceMock.Setup(q => q.ReserveAsync(
                _userId,
                It.IsAny<Guid>(),
                _userId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceCreditOperationResult<UserSubscriptionUsage>.Ok(
                new UserSubscriptionUsage
                {
                    Id = Guid.NewGuid(),
                    UserSubscriptionId = Guid.NewGuid(),
                    QuotaId = Guid.NewGuid()
                }));

        // Act
        var result = await _service.AnalyzeFromDocumentUrlAsync(_userId, req);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(savedSession, Is.Not.Null);
        Assert.That(savedSession.UserId, Is.EqualTo(_userId));
        Assert.That(savedSession.DocumentUrl, Is.EqualTo("http://example.com/test.png"));
        Assert.That(savedSession.Status, Is.EqualTo(LabTestSessionStatus.Processing));
        Assert.That(savedSession.PatientAgeAtTest, Is.EqualTo(25));
        Assert.That(savedSession.PatientGenderAtTest, Is.EqualTo(Gender.Male));

        _schedulerMock.Verify(s => s.EnqueueOcr(savedSession.Id), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ GetSessionAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetSessionAsync_EmptyIds_ReturnsNull()
    {
        // Act & Assert
        Assert.That(await _service.GetSessionAsync(Guid.Empty, Guid.NewGuid()), Is.Null);
        Assert.That(await _service.GetSessionAsync(Guid.NewGuid(), Guid.Empty), Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetSessionAsync_SessionNotFound_ReturnsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _sessionDetailsMock.Setup(r => r.GetByIdWithResultsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabTestSession?)null);

        // Act
        var result = await _service.GetSessionAsync(_userId, sessionId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetSessionAsync_UserIdMismatch_ReturnsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new LabTestSession { Id = sessionId, UserId = Guid.NewGuid() };
        _sessionDetailsMock.Setup(r => r.GetByIdWithResultsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _service.GetSessionAsync(_userId, sessionId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("B")]
    public async Task GetSessionAsync_CompletedSessionWithNoResults_TriggersAnalysis()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionWithoutResults = new LabTestSession
        {
            Id = sessionId,
            UserId = _userId,
            Status = LabTestSessionStatus.Completed,
            RawOcrText = "some raw text",
            LabTestResultDetails = new List<LabTestResultDetail>() // Empty results
        };

        var sessionWithResults = new LabTestSession
        {
            Id = sessionId,
            UserId = _userId,
            Status = LabTestSessionStatus.Completed,
            RawOcrText = "some raw text",
            LabTestResultDetails = new List<LabTestResultDetail>
            {
                new() { Id = Guid.NewGuid(), RawExtractedName = "HGB", UserValue = 14.5 }
            }
        };

        _sessionDetailsMock.SetupSequence(r => r.GetByIdWithResultsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionWithoutResults)
            .ReturnsAsync(sessionWithResults);

        // Act
        var result = await _service.GetSessionAsync(_userId, sessionId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Results, Has.Count.EqualTo(1));
        _analyzerMock.Verify(a => a.AnalyzeAndPersistAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task GetSessionAsync_ValidSession_ReturnsMappedResponse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new LabTestSession
        {
            Id = sessionId,
            UserId = _userId,
            Status = LabTestSessionStatus.Completed,
            DocumentUrl = "http://example.com/test.png",
            PatientGenderAtTest = Gender.Female,
            PatientAgeAtTest = 30,
            TestDate = new DateOnly(2026, 8, 1),
            LabTestResultDetails = new List<LabTestResultDetail>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RawExtractedName = "Glucose",
                    UserValue = 5.5,
                    Status = LabResultStatus.Normal,
                    IsMatched = true,
                    MatchConfidence = 0.95,
                    ReferenceMinUsed = 4.0,
                    ReferenceMaxUsed = 6.0,
                    ReferenceUnitUsed = "mmol/L"
                }
            }
        };

        _sessionDetailsMock.Setup(r => r.GetByIdWithResultsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _service.GetSessionAsync(_userId, sessionId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.SessionId, Is.EqualTo(sessionId));
        Assert.That(result.DocumentUrl, Is.EqualTo(session.DocumentUrl));
        Assert.That(result.PatientGenderAtTest, Is.EqualTo(Gender.Female));
        Assert.That(result.Results, Has.Count.EqualTo(1));
        var item = result.Results[0];
        Assert.That(item.RawExtractedName, Is.EqualTo("Glucose"));
        Assert.That(item.UserValue, Is.EqualTo(5.5));
        Assert.That(item.ReferenceMinUsed, Is.EqualTo(4.0));
        Assert.That(item.ReferenceMaxUsed, Is.EqualTo(6.0));
        Assert.That(item.ReferenceUnitUsed, Is.EqualTo("mmol/L"));
        Assert.That(item.ComparisonTypeUsed, Is.EqualTo(ReferenceComparisonType.Between));
    }

    // â”€â”€ GetSessionsByUserIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetSessionsByUserIdAsync_EmptyUserId_ReturnsEmptyResponse()
    {
        // Act
        var result = await _service.GetSessionsByUserIdAsync(Guid.Empty, null, 1, 10);

        // Assert
        Assert.That(result.Items, Is.Empty);
    }

    [Test]
    [Category("N")]
    public async Task GetSessionsByUserIdAsync_ValidUserId_ReturnsPagedResponse()
    {
        // Arrange
        var pagedResult = new PagedResult<LabTestSession>
        {
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPages = 1,
            Items = new List<LabTestSession>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    UserId = _userId,
                    DocumentUrl = "http://example.com/test.png",
                    Status = LabTestSessionStatus.Completed,
                    TestDate = new DateOnly(2026, 8, 1),
                    PatientGenderAtTest = Gender.Male,
                    PatientAgeAtTest = 45,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        _sessionDetailsMock.Setup(r => r.GetPagedByUserIdAsync(_userId, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _service.GetSessionsByUserIdAsync(_userId, null, 1, 10);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PageNumber, Is.EqualTo(1));
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].SessionId, Is.EqualTo(pagedResult.Items[0].Id));
    }

    // â”€â”€ GetOcrExtractsBySessionIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetOcrExtractsBySessionIdAsync_EmptyIds_ReturnsNull()
    {
        Assert.That(await _service.GetOcrExtractsBySessionIdAsync(Guid.Empty, Guid.NewGuid()), Is.Null);
        Assert.That(await _service.GetOcrExtractsBySessionIdAsync(Guid.NewGuid(), Guid.Empty), Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetOcrExtractsBySessionIdAsync_SessionNotFound_ReturnsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _sessionsMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabTestSession?)null);

        // Act
        var result = await _service.GetOcrExtractsBySessionIdAsync(_userId, sessionId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetOcrExtractsBySessionIdAsync_SessionUserIdMismatch_ReturnsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new LabTestSession { Id = sessionId, UserId = Guid.NewGuid() };
        _sessionsMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _service.GetOcrExtractsBySessionIdAsync(_userId, sessionId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetOcrExtractsBySessionIdAsync_ValidSession_ReturnsExtracts()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new LabTestSession { Id = sessionId, UserId = _userId };
        var extracts = new List<LabTestOcrExtract>
        {
            new() { Id = Guid.NewGuid(), TestSessionId = sessionId, RowIndex = 1, ExtractedTestName = "HGB", ExtractedValue = "14.5" }
        };

        _sessionsMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _ocrExtractsMock.Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<LabTestOcrExtract, bool>>>(),
                It.IsAny<Func<IQueryable<LabTestOcrExtract>, IOrderedQueryable<LabTestOcrExtract>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(extracts);

        // Act
        var result = await _service.GetOcrExtractsBySessionIdAsync(_userId, sessionId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ExtractedTestName, Is.EqualTo("HGB"));
        Assert.That(result[0].ExtractedValue, Is.EqualTo("14.5"));
    }
}
