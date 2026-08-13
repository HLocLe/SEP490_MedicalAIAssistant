using System.Linq.Expressions;
using MedMateAI.Application.DTOs.MedicalDepartments.Responses;
using MedMateAI.Application.IService;
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
public class ConsultationSessionServiceTests
{
    private Mock<IMedicalDepartmentService> _medicalDepartmentServiceMock = null!;
    private Mock<IMedicalFacilityService> _medicalFacilityServiceMock = null!;
    private Mock<IAIConfigService> _aiConfigServiceMock = null!;
    private Mock<IAIChatProvider> _aiChatProviderMock = null!;
    private Mock<IGenericRepository<ConsultationSession>> _sessionsRepoMock = null!;
    private Mock<IGenericRepository<ConsultationQuestion>> _questionsRepoMock = null!;
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IUserService> _userServiceMock = null!;
    private Mock<IChecklistItemService> _checklistItemServiceMock = null!;
    private Mock<ISmsSender> _smsSenderMock = null!;
    private Mock<IConsultationSessionJobScheduler> _jobSchedulerMock = null!;
    private Mock<IConsultationSessionQuotaService> _quotaServiceMock = null!;
    private ConsultationSessionService _service = null!;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _departmentId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _medicalDepartmentServiceMock = new Mock<IMedicalDepartmentService>();
        _medicalFacilityServiceMock = new Mock<IMedicalFacilityService>();
        _aiConfigServiceMock = new Mock<IAIConfigService>();
        _aiChatProviderMock = new Mock<IAIChatProvider>();
        _sessionsRepoMock = new Mock<IGenericRepository<ConsultationSession>>();
        _questionsRepoMock = new Mock<IGenericRepository<ConsultationQuestion>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userServiceMock = new Mock<IUserService>();
        _checklistItemServiceMock = new Mock<IChecklistItemService>();
        _smsSenderMock = new Mock<ISmsSender>();
        _jobSchedulerMock = new Mock<IConsultationSessionJobScheduler>();
        _quotaServiceMock = new Mock<IConsultationSessionQuotaService>();

        _service = new ConsultationSessionService(
            _medicalDepartmentServiceMock.Object,
            _medicalFacilityServiceMock.Object,
            _aiConfigServiceMock.Object,
            _aiChatProviderMock.Object,
            _sessionsRepoMock.Object,
            _questionsRepoMock.Object,
            _unitOfWorkMock.Object,
            _userServiceMock.Object,
            _checklistItemServiceMock.Object,
            _smsSenderMock.Object,
            _jobSchedulerMock.Object,
            _quotaServiceMock.Object);
    }

    private ConsultationSession MakeSession(
        ConsultationSessionStatus status = ConsultationSessionStatus.Completed,
        Guid? userId = null) =>
        new()
        {
            Id = _sessionId,
            UserId = userId ?? _userId,
            DepartmentId = _departmentId,
            UserSymptoms = "Fever and cough",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

    // ── GetMyCompletedSessionsAsync ──────────────────────────────────────────

    [Test]
    [Category("B")]
    public async Task GetMyCompletedSessionsAsync_EmptyUserId_ReturnsEmptyPagedResponse()
    {
        var result = await _service.GetMyCompletedSessionsAsync(Guid.Empty, 1, 10, CancellationToken.None);

        Assert.That(result.Items, Is.Empty);
        _sessionsRepoMock.Verify(r => r.GetPagedAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<Expression<Func<ConsultationSession, bool>>>(),
            It.IsAny<Func<IQueryable<ConsultationSession>, IOrderedQueryable<ConsultationSession>>>(),
            It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("N")]
    public async Task GetMyCompletedSessionsAsync_ValidRequest_ReturnsMappedSessions()
    {
        var session = MakeSession();
        _sessionsRepoMock.Setup(r => r.GetPagedAsync(
                1, 10,
                It.IsAny<Expression<Func<ConsultationSession, bool>>>(),
                It.IsAny<Func<IQueryable<ConsultationSession>, IOrderedQueryable<ConsultationSession>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ConsultationSession>
            {
                PageNumber = 1,
                PageSize = 10,
                TotalCount = 1,
                TotalPages = 1,
                Items = new List<ConsultationSession> { session }
            });
        _medicalDepartmentServiceMock.Setup(m => m.GetMedicalDepartmentByIdAsync(_departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalDepartmentResponse { Id = _departmentId, DepartmentName = "Cardiology" });

        var result = await _service.GetMyCompletedSessionsAsync(_userId, 1, 10, CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].SessionId, Is.EqualTo(_sessionId));
        Assert.That(result.Items[0].DepartmentName, Is.EqualTo("Cardiology"));
    }

    // ── GetConsultationSessionByIdAsync ──────────────────────────────────────

    [Test]
    [Category("B")]
    public async Task GetConsultationSessionByIdAsync_EmptyIds_ReturnsNotFound()
    {
        var (notFound, data) = await _service.GetConsultationSessionByIdAsync(
            Guid.Empty, Guid.Empty, CancellationToken.None);

        Assert.That(notFound, Is.True);
        Assert.That(data, Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetConsultationSessionByIdAsync_SessionNotFound_ReturnsNotFound()
    {
        _sessionsRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<ConsultationSession, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConsultationSession?)null);

        var (notFound, data) = await _service.GetConsultationSessionByIdAsync(
            _userId, _sessionId, CancellationToken.None);

        Assert.That(notFound, Is.True);
        Assert.That(data, Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetConsultationSessionByIdAsync_ValidRequest_ReturnsDetailWithQuestions()
    {
        var session = MakeSession();
        _sessionsRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<ConsultationSession, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _medicalDepartmentServiceMock.Setup(m => m.GetMedicalDepartmentByIdAsync(_departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalDepartmentResponse { Id = _departmentId, DepartmentName = "Cardiology" });
        var question = new ConsultationQuestion
        {
            Id = Guid.NewGuid(),
            ConsultationSessionId = _sessionId,
            QuestionText = "Any chest pain?",
            Category = "Symptom",
            Priority = 0
        };
        _questionsRepoMock.Setup(r => r.GetPagedAsync(
                1, 100,
                It.IsAny<Expression<Func<ConsultationQuestion, bool>>>(),
                It.IsAny<Func<IQueryable<ConsultationQuestion>, IOrderedQueryable<ConsultationQuestion>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ConsultationQuestion>
            {
                PageNumber = 1,
                PageSize = 100,
                TotalCount = 1,
                TotalPages = 1,
                Items = new List<ConsultationQuestion> { question }
            });

        var (notFound, data) = await _service.GetConsultationSessionByIdAsync(
            _userId, _sessionId, CancellationToken.None);

        Assert.That(notFound, Is.False);
        Assert.That(data!.SessionId, Is.EqualTo(_sessionId));
        Assert.That(data.DepartmentName, Is.EqualTo("Cardiology"));
        Assert.That(data.Questions, Has.Count.EqualTo(1));
        Assert.That(data.Questions[0].QuestionText, Is.EqualTo("Any chest pain?"));
    }
}
