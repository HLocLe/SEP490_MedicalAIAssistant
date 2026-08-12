using AutoMapper;
using MedMateAI.Application.DTOs.SymptomAnalysis.Requests;
using MedMateAI.Application.DTOs.SymptomAnalysis.Responses.ClinicalQuestions;
using MedMateAI.Application.DTOs.SymptomAnalysis.Responses.Session;
using MedMateAI.Application.DTOs.Users.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Payments;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class SymptomAnalysisServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<ISymptomAnalysisSessionRepository> _sessionsMock = null!;
    private Mock<IIcdChapterRepository> _chaptersMock = null!;
    private Mock<IClinicalQuestionRepository> _questionsMock = null!;
    private Mock<ISessionClinicalQuestionAnswerRepository> _sessionAnswersMock = null!;
    private Mock<IDepartmentRecommendationRepository> _recommendationsMock = null!;
    private Mock<ISessionSymptomRepository> _sessionSymptomsMock = null!;
    private Mock<IMedicalDepartmentRepository> _medicalDeptsMock = null!;
    private Mock<IMedicalFacilityRepository> _facilitiesMock = null!;

    private Mock<IUserService> _userServiceMock = null!;
    private Mock<ITranslationService> _translationServiceMock = null!;
    private Mock<IMedGemmaChatService> _medGemmaMock = null!;
    private Mock<IIcdLookupService> _icdLookupMock = null!;
    private Mock<IMapper> _mapperMock = null!;
    private Mock<ILogger<SymptomAnalysisService>> _loggerMock = null!;

    private SymptomAnalysisService _service = null!;
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _sessionsMock = new Mock<ISymptomAnalysisSessionRepository>();
        _chaptersMock = new Mock<IIcdChapterRepository>();
        _questionsMock = new Mock<IClinicalQuestionRepository>();
        _sessionAnswersMock = new Mock<ISessionClinicalQuestionAnswerRepository>();
        _recommendationsMock = new Mock<IDepartmentRecommendationRepository>();
        _sessionSymptomsMock = new Mock<ISessionSymptomRepository>();
        _medicalDeptsMock = new Mock<IMedicalDepartmentRepository>();
        _facilitiesMock = new Mock<IMedicalFacilityRepository>();

        _userServiceMock = new Mock<IUserService>();
        _translationServiceMock = new Mock<ITranslationService>();
        _medGemmaMock = new Mock<IMedGemmaChatService>();
        _icdLookupMock = new Mock<IIcdLookupService>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<SymptomAnalysisService>>();

        _unitOfWorkMock.Setup(u => u.SymptomAnalysisSessions).Returns(_sessionsMock.Object);
        _unitOfWorkMock.Setup(u => u.IcdChapters).Returns(_chaptersMock.Object);
        _unitOfWorkMock.Setup(u => u.ClinicalQuestions).Returns(_questionsMock.Object);
        _unitOfWorkMock.Setup(u => u.SessionClinicalQuestionAnswers).Returns(_sessionAnswersMock.Object);
        _unitOfWorkMock.Setup(u => u.DepartmentRecommendations).Returns(_recommendationsMock.Object);
        _unitOfWorkMock.Setup(u => u.SessionSymptoms).Returns(_sessionSymptomsMock.Object);
        _unitOfWorkMock.Setup(u => u.MedicalDepartments).Returns(_medicalDeptsMock.Object);
        _unitOfWorkMock.Setup(u => u.MedicalFacilities).Returns(_facilitiesMock.Object);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _service = new SymptomAnalysisService(
            _unitOfWorkMock.Object,
            _userServiceMock.Object,
            _translationServiceMock.Object,
            _medGemmaMock.Object,
            _icdLookupMock.Object,
            _mapperMock.Object,
            _loggerMock.Object);
    }

    // ── GetSessionByIdAsync ──────────────────────────────────────────────────

    [Test]
    [Category("B")]
    public async Task GetSessionByIdAsync_EmptyId_ReturnsNull()
    {
        Assert.That(await _service.GetSessionByIdAsync(Guid.Empty), Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetSessionByIdAsync_SessionNotFound_ReturnsNull()
    {
        _sessionsMock.Setup(r => r.GetByIdAsync(_sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SymptomAnalysisSession?)null);

        Assert.That(await _service.GetSessionByIdAsync(_sessionId), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetSessionByIdAsync_ValidSession_ReturnsMappedResponse()
    {
        // Arrange
        var session = new SymptomAnalysisSession { Id = _sessionId, UserId = _userId, Status = SymptomAnalysisSessionStatus.Completed };
        _userServiceMock.Setup(service => service.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationUserResponse { Id = _userId });
        _userServiceMock.Setup(service => service.IsInRoleAsync(
                _userId,
                "Admin",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _sessionsMock.Setup(r => r.GetByIdAsync(_sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var symptomsPaged = new PagedResult<SessionSymptom> { Items = new List<SessionSymptom>() };
        _sessionSymptomsMock.Setup(r => r.GetPagedAsync(1, 100, It.IsAny<System.Linq.Expressions.Expression<Func<SessionSymptom, bool>>>(), It.IsAny<Func<IQueryable<SessionSymptom>, IOrderedQueryable<SessionSymptom>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(symptomsPaged);

        var answers = new List<SessionClinicalQuestionAnswer>();
        _sessionAnswersMock.Setup(r => r.GetBySessionIdAsync(_sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(answers);

        var recsPaged = new PagedResult<DepartmentRecommendation> { Items = new List<DepartmentRecommendation>() };
        _recommendationsMock.Setup(r => r.GetPagedAsync(1, 50, It.IsAny<System.Linq.Expressions.Expression<Func<DepartmentRecommendation, bool>>>(), It.IsAny<Func<IQueryable<DepartmentRecommendation>, IOrderedQueryable<DepartmentRecommendation>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recsPaged);

        _mapperMock.Setup(m => m.Map<SymptomAnalysisResponse>(session))
            .Returns(new SymptomAnalysisResponse { SessionId = _sessionId });

        // Act
        var result = await _service.GetSessionByIdAsync(_sessionId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.SessionId, Is.EqualTo(_sessionId));
    }

    // ── GetSessionsByUserIdAsync ─────────────────────────────────────────────

    [Test]
    [Category("B")]
    public async Task GetSessionsByUserIdAsync_EmptyId_ReturnsEmptyResponse()
    {
        var result = await _service.GetSessionsByUserIdAsync(Guid.Empty, null, 1, 10);
        Assert.That(result.Items, Is.Empty);
    }

    // ── SuggestClinicalQuestionAsync ─────────────────────────────────────────

    [Test]
    [Category("B")]
    public void SuggestClinicalQuestionAsync_EmptyInput_ThrowsArgumentException()
    {
        var req = new SuggestClinicalQuestionRequest { UserInput = "  " };
        var exception = Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SuggestClinicalQuestionAsync(req));

        Assert.That(exception!.Message, Is.EqualTo("Nội dung triệu chứng là bắt buộc"));
    }

    [Test]
    [Category("B")]
    public async Task SuggestClinicalQuestionAsync_InputTooLong_ThrowsArgumentException()
    {
        var req = new SuggestClinicalQuestionRequest { UserInput = new string('x', 2001) };
        Assert.ThrowsAsync<ArgumentException>(() => _service.SuggestClinicalQuestionAsync(req));
    }

    [Test]
    [Category("N")]
    public async Task SuggestClinicalQuestionAsync_ValidInput_MatchesChaptersAndSuggestsQuestions()
    {
        // Arrange
        var req = new SuggestClinicalQuestionRequest { UserInput = "Đau đầu" };
        var chapters = new List<IcdChapter>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChapterCode = "C1",
                KeywordWeights = new Dictionary<string, int> { ["Đau"] = 5 }
            }
        };

        _chaptersMock.Setup(r => r.GetActiveChaptersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapters);

        var questions = new List<ClinicalQuestion>
        {
            new()
            {
                Id = Guid.NewGuid(),
                QuestionVi = "Bạn có bị đau đầu nhiều không?",
                ChapterId = chapters[0].Id,
                Answers = new Dictionary<string, string> { ["có"] = "headache" }
            }
        };

        _questionsMock.Setup(r => r.GetQuestionsByChapterIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(questions);

        // Act
        var result = await _service.SuggestClinicalQuestionAsync(req);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Questions, Has.Count.EqualTo(1));
        Assert.That(result.Questions[0].QuestionVi, Is.EqualTo("Bạn có bị đau đầu nhiều không?"));

        _sessionAnswersMock.Verify(r => r.Add(It.IsAny<SessionClinicalQuestionAnswer>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
