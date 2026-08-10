using MedMateAI.Application.Common;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.RecoveryPlans;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanClinicalContextServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IRecoveryPlanRequestRepository> _requestRepoMock = null!;
    private Mock<IRecoveryPlanRepository> _planRepoMock = null!;
    private RecoveryPlanClinicalContextService _service = null!;
    private readonly Guid _doctorId = Guid.NewGuid();
    private readonly Guid _doctorUserId = Guid.NewGuid();
    private readonly Guid _requestId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _requestRepoMock = new Mock<IRecoveryPlanRequestRepository>();
        _planRepoMock = new Mock<IRecoveryPlanRepository>();

        _unitOfWorkMock.Setup(u => u.RecoveryPlanRequests).Returns(_requestRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.RecoveryPlans).Returns(_planRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new RecoveryPlanClinicalContextService(_unitOfWorkMock.Object);
    }

    private Doctor MakeDoctor(bool isActive = true) =>
        new() { Id = _doctorId, UserId = _doctorUserId, IsActive = isActive };

    private RecoveryPlanRequest MakeRequest(
        RecoveryPlanRequestStatus status = RecoveryPlanRequestStatus.InReview,
        Guid? assignedDoctorId = null,
        DateTime? assignmentExpiresAt = null) =>
        new()
        {
            Id = _requestId,
            Status = status,
            AssignedDoctorId = assignedDoctorId ?? _doctorId,
            AssignmentExpiresAt = assignmentExpiresAt
        };

    private RecoveryPlanClinicalContextData MakeContextData(
        RecoveryPlanRequestStatus status = RecoveryPlanRequestStatus.InReview,
        Guid? assignedDoctorId = null) =>
        new(
            _requestId,
            Guid.NewGuid(),
            assignedDoctorId ?? _doctorId,
            RecoveryPlanDiseaseGroup.Respiratory,
            status,
            null,
            null,
            DateTime.UtcNow,
            null,
            new RecoveryPlanPatientProfileData(Guid.NewGuid(), 180, 75, "None", DateTime.UtcNow, null),
            Array.Empty<RecoveryPlanChronicDiseaseData>(),
            null,
            Array.Empty<RecoveryPlanUserMedicationData>(),
            null);

    // ── GetForDoctorAsync ────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task GetForDoctorAsync_DoctorNotFound_ReturnsDoctorProfileNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var result = await _service.GetForDoctorAsync(_doctorUserId, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.DoctorProfileNotFound));
    }

    [Test]
    [Category("A")]
    public async Task GetForDoctorAsync_RequestNotAssignedToDoctor_ReturnsNotFound()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest(assignedDoctorId: Guid.NewGuid()));

        var result = await _service.GetForDoctorAsync(_doctorUserId, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("B")]
    public async Task GetForDoctorAsync_AssignmentExpired_ReturnsAssignmentExpired()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest(
                RecoveryPlanRequestStatus.Assigned,
                assignmentExpiresAt: DateTime.UtcNow.AddMinutes(-1)));

        var result = await _service.GetForDoctorAsync(_doctorUserId, _requestId, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.AssignmentExpired));
    }

    [Test]
    [Category("N")]
    public async Task GetForDoctorAsync_ValidRequest_ReturnsClinicalContext()
    {
        _requestRepoMock.Setup(r => r.GetDoctorByUserIdAsync(_doctorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDoctor());
        _requestRepoMock.Setup(r => r.GetByIdForUpdateAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRequest());
        _planRepoMock.Setup(r => r.GetClinicalContextAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeContextData());

        var result = await _service.GetForDoctorAsync(_doctorUserId, _requestId, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.RequestId, Is.EqualTo(_requestId));
        Assert.That(result.Data.PatientProfile!.Bmi, Is.Not.Null);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── BuildSnapshotAsync ───────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task BuildSnapshotAsync_ContextNotFound_ReturnsNull()
    {
        _planRepoMock.Setup(r => r.GetClinicalContextAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryPlanClinicalContextData?)null);

        var result = await _service.BuildSnapshotAsync(_requestId, DateTime.UtcNow, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task BuildSnapshotAsync_ValidRequest_ReturnsSnapshotWithMappedFields()
    {
        var capturedAt = DateTime.UtcNow;
        _planRepoMock.Setup(r => r.GetClinicalContextAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeContextData());

        var result = await _service.BuildSnapshotAsync(_requestId, capturedAt, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.RequestId, Is.EqualTo(_requestId));
        Assert.That(result.CapturedAtUtc, Is.EqualTo(capturedAt));
        Assert.That(result.PatientProfile!.Bmi, Is.Not.Null);
    }

    [Test]
    [Category("N")]
    public void SerializeSnapshot_ValidSnapshot_ReturnsCamelCaseJson()
    {
        // Arrange
        var snapshot = new RecoveryPlanClinicalSnapshot
        {
            SchemaVersion = 1,
            CapturedAtUtc = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc),
            RequestId = Guid.NewGuid(),
            DiseaseGroup = RecoveryPlanDiseaseGroup.Respiratory,
            PatientProfile = new RecoveryPlanSnapshotPatientProfile
            {
                HeightCm = 180,
                WeightKg = 75,
                Bmi = 23.15
            }
        };

        // Act
        var json = _service.SerializeSnapshot(snapshot);

        // Assert
        Assert.That(json, Is.Not.Null);
        Assert.That(json, Does.Contain("\"schemaVersion\":1"));
        Assert.That(json, Does.Contain("\"diseaseGroup\":\"Respiratory\""));
        Assert.That(json, Does.Contain("\"heightCm\":180"));
    }

    [Test]
    [Category("N")]
    public void DeserializeSnapshot_ValidJson_ReturnsDeserializedSnapshot()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var json = $"{{\"schemaVersion\":1,\"capturedAtUtc\":\"2026-08-03T12:00:00Z\",\"requestId\":\"{requestId}\",\"diseaseGroup\":\"Respiratory\",\"patientProfile\":{{\"heightCm\":180,\"weightKg\":75,\"bmi\":23.15}}}}";

        // Act
        var result = _service.DeserializeSnapshot(json);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.SchemaVersion, Is.EqualTo(1));
        Assert.That(result.RequestId, Is.EqualTo(requestId));
        Assert.That(result.DiseaseGroup, Is.EqualTo(RecoveryPlanDiseaseGroup.Respiratory));
        Assert.That(result.PatientProfile!.HeightCm, Is.EqualTo(180));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [Category("B")]
    public void DeserializeSnapshot_NullOrEmptyJson_ReturnsNull(string? json)
    {
        // Act
        var result = _service.DeserializeSnapshot(json);

        // Assert
        Assert.That(result, Is.Null);
    }
}
