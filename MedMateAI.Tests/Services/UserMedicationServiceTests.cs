using MedMateAI.Application.DTOs.UserMedications;
using MedMateAI.Application.Models.UserMedications;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class UserMedicationServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IUserMedicationRepository> _repoMock = null!;
    private UserMedicationService _service = null!;
    private readonly Guid _userId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _repoMock = new Mock<IUserMedicationRepository>();
        _unitOfWorkMock.Setup(u => u.UserMedications).Returns(_repoMock.Object);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _service = new UserMedicationService(_unitOfWorkMock.Object);
    }

    // ── CreateAsync — validation failures (no DB call needed) ─────────────────

    [Test]
    [Category("B")]
    public async Task CreateAsync_EmptyMedicineName_ReturnsInvalidRequest()
    {
        var req = new CreateUserMedicationRequest { MedicineName = "" };
        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.InvalidRequest));
    }

    [Test]
    [Category("B")]
    public async Task CreateAsync_MedicineNameTooLong_ReturnsInvalidRequest()
    {
        var req = new CreateUserMedicationRequest { MedicineName = new string('x', 257) };
        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.InvalidRequest));
    }

    [Test]
    [Category("B")]
    public async Task CreateAsync_DosageInstructionTooLong_ReturnsInvalidRequest()
    {
        var req = new CreateUserMedicationRequest
        {
            MedicineName = "Aspirin",
            DosageInstruction = new string('x', 1001)
        };
        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.InvalidRequest));
    }

    [Test]
    [Category("B")]
    public async Task CreateAsync_EndDateBeforeStartDate_ReturnsInvalidRequest()
    {
        var req = new CreateUserMedicationRequest
        {
            MedicineName = "Aspirin",
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 5, 1)
        };
        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.InvalidRequest));
    }

    [Test]
    [Category("B")]
    public async Task CreateAsync_TooManyReminderTimes_ReturnsInvalidRequest()
    {
        var times = Enumerable.Range(0, 13)
            .Select(i => new TimeOnly(i, 0))
            .ToList();
        var req = new CreateUserMedicationRequest
        {
            MedicineName = "Aspirin",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsReminderEnabled = true,
            ReminderTimes = times
        };
        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.InvalidRequest));
    }

    [Test]
    [Category("A")]
    public async Task CreateAsync_DuplicateReminderTimes_ReturnsInvalidRequest()
    {
        var duplicate = new TimeOnly(8, 0);
        var req = new CreateUserMedicationRequest
        {
            MedicineName = "Aspirin",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsReminderEnabled = true,
            ReminderTimes = new[] { duplicate, duplicate }
        };
        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.InvalidRequest));
    }

    [Test]
    [Category("A")]
    public async Task CreateAsync_ReminderTimeNotMinutePrecision_ReturnsInvalidRequest()
    {
        var subMinuteTime = new TimeOnly(8, 0, 30);   // has seconds
        var req = new CreateUserMedicationRequest
        {
            MedicineName = "Aspirin",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsReminderEnabled = true,
            ReminderTimes = new[] { subMinuteTime }
        };
        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.InvalidRequest));
    }

    [Test]
    [Category("A")]
    public async Task CreateAsync_ReminderEnabledWithNoStartDate_ReturnsInvalidRequest()
    {
        var req = new CreateUserMedicationRequest
        {
            MedicineName = "Aspirin",
            EndDate = new DateOnly(2026, 12, 31),
            IsReminderEnabled = true,
            ReminderTimes = new[] { new TimeOnly(8, 0) }
        };
        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.InvalidRequest));
    }

    [Test]
    [Category("B")]
    public async Task CreateAsync_ReminderEnabledWithNoReminderTimes_ReturnsInvalidRequest()
    {
        var req = new CreateUserMedicationRequest
        {
            MedicineName = "Aspirin",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsReminderEnabled = true,
            ReminderTimes = Array.Empty<TimeOnly>()
        };
        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.InvalidRequest));
    }

    // ── CreateAsync — success paths ───────────────────────────────────────────

    [Test]
    [Category("N")]
    public async Task CreateAsync_ValidNoReminder_ReturnsSuccessAndCallsSave()
    {
        UserMedication? added = null;
        _repoMock.Setup(r => r.Add(It.IsAny<UserMedication>()))
            .Callback<UserMedication>(m => added = m);

        var req = new CreateUserMedicationRequest
        {
            MedicineName = "  Aspirin  ",
            DosageInstruction = "1 tablet daily",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Success, Is.True);
        Assert.That(added, Is.Not.Null);
        Assert.That(added!.MedicineName, Is.EqualTo("Aspirin"));     // trimmed
        Assert.That(added.UserId, Is.EqualTo(_userId));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task CreateAsync_ValidWithReminders_AddsReminderTimes()
    {
        UserMedication? added = null;
        _repoMock.Setup(r => r.Add(It.IsAny<UserMedication>()))
            .Callback<UserMedication>(m => added = m);

        var req = new CreateUserMedicationRequest
        {
            MedicineName = "Metformin",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsReminderEnabled = true,
            ReminderTimes = new[] { new TimeOnly(8, 0), new TimeOnly(20, 0) }
        };

        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Success, Is.True);
        Assert.That(added!.IsReminderEnabled, Is.True);
        Assert.That(added.ReminderTimes, Has.Count.EqualTo(2));
        Assert.That(added.ReminderTimes.All(t => t.IsActive), Is.True);
    }

    [Test]
    [Category("B")]
    public async Task CreateAsync_ValidWithNoEndDate_Succeeds()
    {
        _repoMock.Setup(r => r.Add(It.IsAny<UserMedication>()));

        var req = new CreateUserMedicationRequest { MedicineName = "Vitamin D" };
        var result = await _service.CreateAsync(_userId, req);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.EndDate, Is.Null);
    }

    // ── DeleteAsync ────────────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task DeleteAsync_MedicationNotFound_ReturnsNotFound()
    {
        _repoMock.Setup(r => r.GetByIdForUpdateAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMedication?)null);

        var result = await _service.DeleteAsync(_userId, Guid.NewGuid());

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.NotFound));
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("N")]
    public async Task DeleteAsync_Found_SoftDeletesMedicationAndDeactivatesReminders()
    {
        var medication = new UserMedication
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MedicineName = "Aspirin",
            IsReminderEnabled = true,
            ReminderTimes = new List<UserMedicationReminderTime>
            {
                new() { Id = Guid.NewGuid(), TimeOfDay = new TimeOnly(8, 0), IsActive = true }
            }
        };

        _repoMock.Setup(r => r.GetByIdForUpdateAsync(_userId, medication.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medication);

        var result = await _service.DeleteAsync(_userId, medication.Id);

        Assert.That(result.Success, Is.True);
        Assert.That(medication.IsDeleted, Is.True);
        Assert.That(medication.IsReminderEnabled, Is.False);
        Assert.That(medication.ReminderTimes.First().IsActive, Is.False);
        Assert.That(medication.ReminderTimes.First().IsDeleted, Is.True);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetByIdAsync ───────────────────────────────────────────────────────────

    [Test]
    [Category("A")]
    public async Task GetByIdAsync_NotFound_ReturnsNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMedication?)null);

        var result = await _service.GetByIdAsync(_userId, Guid.NewGuid());

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public async Task GetByIdAsync_Found_ReturnsMappedResponse()
    {
        var id = Guid.NewGuid();
        var medication = new UserMedication
        {
            Id = id,
            UserId = _userId,
            MedicineName = "Ibuprofen",
            ReminderTimes = new List<UserMedicationReminderTime>()
        };

        _repoMock.Setup(r => r.GetByIdAsync(_userId, id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medication);

        var result = await _service.GetByIdAsync(_userId, id);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.MedicineName, Is.EqualTo("Ibuprofen"));
        Assert.That(result.Data.Id, Is.EqualTo(id));
    }

    // ── GetMineAsync ───────────────────────────────────────────────────────────

    [Test]
    [Category("N")]
    public async Task GetMineAsync_ValidUserId_ReturnsUserMedications()
    {
        var meds = new List<UserMedication>
        {
            new() { Id = Guid.NewGuid(), UserId = _userId, MedicineName = "Med1" },
            new() { Id = Guid.NewGuid(), UserId = _userId, MedicineName = "Med2" }
        };

        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(meds);

        var result = await _service.GetMineAsync(_userId);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Has.Count.EqualTo(2));
        Assert.That(result.Data[0].MedicineName, Is.EqualTo("Med1"));
        Assert.That(result.Data[1].MedicineName, Is.EqualTo("Med2"));
    }

    [Test]
    [Category("B")]
    public async Task GetMineAsync_NoMedications_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserMedication>());

        var result = await _service.GetMineAsync(_userId);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Is.Empty);
    }

    // ── UpdateAsync ────────────────────────────────────────────────────────────

    [Test]
    [Category("N")]
    public async Task UpdateAsync_ValidRequest_UpdatesAndReturnsResponse()
    {
        var medId = Guid.NewGuid();
        var med = new UserMedication
        {
            Id = medId,
            UserId = _userId,
            MedicineName = "OldName",
            IsReminderEnabled = false
        };

        _repoMock.Setup(r => r.GetByIdForUpdateAsync(_userId, medId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(med);

        var req = new UpdateUserMedicationRequest
        {
            MedicineName = "NewName",
            DosageInstruction = "Take 2",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 1, 10),
            IsReminderEnabled = false
        };

        var result = await _service.UpdateAsync(_userId, medId, req);

        Assert.That(result.Success, Is.True);
        Assert.That(med.MedicineName, Is.EqualTo("NewName"));
        Assert.That(med.DosageInstruction, Is.EqualTo("Take 2"));
        Assert.That(med.StartDate, Is.EqualTo(new DateOnly(2026, 1, 1)));
        Assert.That(med.EndDate, Is.EqualTo(new DateOnly(2026, 1, 10)));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task UpdateAsync_MedicationNotFound_ReturnsNotFoundAndRollsBack()
    {
        _repoMock.Setup(r => r.GetByIdForUpdateAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMedication?)null);

        var req = new UpdateUserMedicationRequest { MedicineName = "Aspirin" };
        var result = await _service.UpdateAsync(_userId, Guid.NewGuid(), req);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.NotFound));
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("B")]
    public async Task UpdateAsync_InvalidRequestNameEmpty_ReturnsInvalidRequest()
    {
        var req = new UpdateUserMedicationRequest { MedicineName = "" };
        var result = await _service.UpdateAsync(_userId, Guid.NewGuid(), req);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.InvalidRequest));
    }

    [Test]
    [Category("N")]
    public async Task UpdateAsync_ReminderTimesSynchronization_UpdatesCorrectly()
    {
        var medId = Guid.NewGuid();
        var existingReminder = new UserMedicationReminderTime
        {
            Id = Guid.NewGuid(),
            TimeOfDay = new TimeOnly(8, 0),
            IsActive = true,
            IsDeleted = false
        };
        var med = new UserMedication
        {
            Id = medId,
            UserId = _userId,
            MedicineName = "Aspirin",
            IsReminderEnabled = true,
            ReminderTimes = new List<UserMedicationReminderTime> { existingReminder }
        };

        _repoMock.Setup(r => r.GetByIdForUpdateAsync(_userId, medId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(med);

        var req = new UpdateUserMedicationRequest
        {
            MedicineName = "Aspirin",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 1, 10),
            IsReminderEnabled = true,
            ReminderTimes = new[] { new TimeOnly(8, 0), new TimeOnly(12, 0) } // keep 8:00, add 12:00
        };

        var result = await _service.UpdateAsync(_userId, medId, req);

        Assert.That(result.Success, Is.True);
        Assert.That(med.ReminderTimes, Has.Count.EqualTo(2));
        Assert.That(existingReminder.IsDeleted, Is.False); // retained
        Assert.That(med.ReminderTimes.Any(r => r.TimeOfDay == new TimeOnly(12, 0)), Is.True); // added
    }

    // ── ReplaceReminderTimesAsync ──────────────────────────────────────────────

    [Test]
    [Category("N")]
    public async Task ReplaceReminderTimesAsync_ValidRequest_ReplacesTimes()
    {
        var medId = Guid.NewGuid();
        var med = new UserMedication
        {
            Id = medId,
            UserId = _userId,
            MedicineName = "Aspirin",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 1, 10),
            IsReminderEnabled = true,
            ReminderTimes = new List<UserMedicationReminderTime>()
        };

        _repoMock.Setup(r => r.GetByIdForUpdateAsync(_userId, medId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(med);

        var req = new ReplaceMedicationReminderTimesRequest
        {
            IsReminderEnabled = true,
            ReminderTimes = new[] { new TimeOnly(14, 0) }
        };

        var result = await _service.ReplaceReminderTimesAsync(_userId, medId, req);

        Assert.That(result.Success, Is.True);
        Assert.That(med.ReminderTimes, Has.Count.EqualTo(1));
        Assert.That(med.ReminderTimes.First().TimeOfDay, Is.EqualTo(new TimeOnly(14, 0)));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task ReplaceReminderTimesAsync_MedicationNotFound_ReturnsNotFoundAndRollsBack()
    {
        _repoMock.Setup(r => r.GetByIdForUpdateAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMedication?)null);

        var req = new ReplaceMedicationReminderTimesRequest
        {
            IsReminderEnabled = true,
            ReminderTimes = new[] { new TimeOnly(14, 0) }
        };

        var result = await _service.ReplaceReminderTimesAsync(_userId, Guid.NewGuid(), req);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.NotFound));
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task ReplaceReminderTimesAsync_DuplicateReminderTimes_ReturnsInvalidRequestAndRollsBack()
    {
        var medId = Guid.NewGuid();
        var med = new UserMedication
        {
            Id = medId,
            UserId = _userId,
            MedicineName = "Aspirin",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 1, 10),
            IsReminderEnabled = true,
            ReminderTimes = new List<UserMedicationReminderTime>()
        };

        _repoMock.Setup(r => r.GetByIdForUpdateAsync(_userId, medId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(med);

        var req = new ReplaceMedicationReminderTimesRequest
        {
            IsReminderEnabled = true,
            ReminderTimes = new[] { new TimeOnly(14, 0), new TimeOnly(14, 0) }
        };

        var result = await _service.ReplaceReminderTimesAsync(_userId, medId, req);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(UserMedicationErrorCode.InvalidRequest));
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
