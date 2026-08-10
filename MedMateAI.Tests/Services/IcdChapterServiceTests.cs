using AutoMapper;
using MedMateAI.Application.DTOs.IcdChapters.Requests;
using MedMateAI.Application.DTOs.IcdChapters.Responses;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class IcdChapterServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IIcdChapterRepository> _chapterRepoMock = null!;
    private Mock<ISymptomAnalysisSessionRepository> _sessionRepoMock = null!;
    private Mock<IMedicalDepartmentRepository> _departmentRepoMock = null!;
    private Mock<IClinicalQuestionRepository> _questionRepoMock = null!;
    private Mock<IMapper> _mapperMock = null!;
    private IcdChapterService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _chapterRepoMock = new Mock<IIcdChapterRepository>();
        _sessionRepoMock = new Mock<ISymptomAnalysisSessionRepository>();
        _departmentRepoMock = new Mock<IMedicalDepartmentRepository>();
        _questionRepoMock = new Mock<IClinicalQuestionRepository>();
        _mapperMock = new Mock<IMapper>();

        _unitOfWorkMock.Setup(u => u.IcdChapters).Returns(_chapterRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.SymptomAnalysisSessions).Returns(_sessionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.MedicalDepartments).Returns(_departmentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.ClinicalQuestions).Returns(_questionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _mapperMock.Setup(m => m.Map<IcdChapterResponse>(It.IsAny<IcdChapter>()))
            .Returns((IcdChapter src) => new IcdChapterResponse
            {
                Id = src.Id,
                ChapterCode = src.ChapterCode,
                ChapterName = src.ChapterName,
                KeywordWeights = src.KeywordWeights,
                CreatedAt = src.CreatedAt,
                UpdatedAt = src.UpdatedAt,
            });

        _service = new IcdChapterService(_unitOfWorkMock.Object, _mapperMock.Object);
    }

    // â”€â”€ ListIcdChaptersAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task ListIcdChaptersAsync_ValidRequest_ReturnsPagedResponse()
    {
        var chapter = new IcdChapter { Id = Guid.NewGuid(), ChapterCode = "A00", ChapterName = "Cholera" };
        var pagedResult = new PagedResult<IcdChapter>
        {
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPages = 1,
            Items = new List<IcdChapter> { chapter },
        };

        _chapterRepoMock.Setup(r => r.GetPagedAsync(
                1, 10,
                It.IsAny<System.Linq.Expressions.Expression<Func<IcdChapter, bool>>>(),
                It.IsAny<Func<IQueryable<IcdChapter>, IOrderedQueryable<IcdChapter>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _service.ListIcdChaptersAsync(1, 10);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].ChapterCode, Is.EqualTo("A00"));
    }

    // â”€â”€ GetIcdChapterByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetIcdChapterByIdAsync_EmptyId_ReturnsNull()
    {
        Assert.That(await _service.GetIcdChapterByIdAsync(Guid.Empty), Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetIcdChapterByIdAsync_NotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _chapterRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IcdChapter?)null);

        Assert.That(await _service.GetIcdChapterByIdAsync(id), Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetIcdChapterByIdAsync_Deleted_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _chapterRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IcdChapter { Id = id, IsDeleted = true });

        Assert.That(await _service.GetIcdChapterByIdAsync(id), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetIcdChapterByIdAsync_Found_ReturnsResponse()
    {
        var id = Guid.NewGuid();
        _chapterRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IcdChapter { Id = id, ChapterCode = "B00", ChapterName = "Herpes", IsDeleted = false });

        var result = await _service.GetIcdChapterByIdAsync(id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ChapterCode, Is.EqualTo("B00"));
    }

    // â”€â”€ CreateIcdChapterAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("A")]
    public async Task CreateIcdChapterAsync_NullRequest_ReturnsError()
    {
        var (succeeded, errors, data) = await _service.CreateIcdChapterAsync(null!);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Request body is required."));
    }

    [Test]
    [Category("B")]
    public async Task CreateIcdChapterAsync_EmptyFields_ReturnsErrors()
    {
        var request = new CreateIcdChapterRequest { ChapterCode = " ", ChapterName = " " };

        var (succeeded, errors, data) = await _service.CreateIcdChapterAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Chapter code is required."));
        Assert.That(errors, Contains.Item("Chapter name is required."));
    }

    [Test]
    [Category("A")]
    public async Task CreateIcdChapterAsync_DuplicateCode_ReturnsError()
    {
        var request = new CreateIcdChapterRequest { ChapterCode = "a00", ChapterName = "Cholera" };

        _chapterRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<IcdChapter, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IcdChapter { ChapterCode = "A00" });

        var (succeeded, errors, data) = await _service.CreateIcdChapterAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Chapter code already exists."));
    }

    [Test]
    [Category("N")]
    public async Task CreateIcdChapterAsync_ValidRequest_CreatesAndReturnsResponse()
    {
        var request = new CreateIcdChapterRequest
        {
            ChapterCode = " a00 ",
            ChapterName = " Cholera ",
            KeywordWeights = new Dictionary<string, int> { { " Fever ", 5 } },
        };

        _chapterRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<IcdChapter, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IcdChapter?)null);

        var (succeeded, errors, data) = await _service.CreateIcdChapterAsync(request);

        Assert.That(succeeded, Is.True);
        Assert.That(data, Is.Not.Null);
        Assert.That(data!.ChapterCode, Is.EqualTo("A00"));
        Assert.That(data.ChapterName, Is.EqualTo("Cholera"));
        _chapterRepoMock.Verify(r => r.Add(It.IsAny<IcdChapter>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ BulkCreateIcdChaptersAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task BulkCreateIcdChaptersAsync_EmptyChapters_ReturnsError()
    {
        var request = new BulkCreateIcdChaptersRequest { Chapters = new List<CreateIcdChapterRequest>() };

        var (succeeded, errors, data) = await _service.BulkCreateIcdChaptersAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("At least one chapter is required."));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateIcdChaptersAsync_NullItem_ReturnsIndexedError()
    {
        var request = new BulkCreateIcdChaptersRequest
        {
            Chapters = new List<CreateIcdChapterRequest> { null! },
        };

        var (succeeded, errors, data) = await _service.BulkCreateIcdChaptersAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Chapters[0]: Item is required."));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateIcdChaptersAsync_DuplicateCodeWithinRequest_ReturnsError()
    {
        var request = new BulkCreateIcdChaptersRequest
        {
            Chapters = new List<CreateIcdChapterRequest>
            {
                new() { ChapterCode = "A00", ChapterName = "Cholera" },
                new() { ChapterCode = "a00", ChapterName = "Cholera dup" },
            },
        };

        var (succeeded, errors, data) = await _service.BulkCreateIcdChaptersAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors.Any(e => e.Contains("Duplicate chapter code")), Is.True);
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateIcdChaptersAsync_CodeExistsInDb_ReturnsError()
    {
        var request = new BulkCreateIcdChaptersRequest
        {
            Chapters = new List<CreateIcdChapterRequest>
            {
                new() { ChapterCode = "A00", ChapterName = "Cholera" },
            },
        };

        _chapterRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IcdChapter> { new() { ChapterCode = "A00", IsDeleted = false } });

        var (succeeded, errors, data) = await _service.BulkCreateIcdChaptersAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors.Any(e => e.Contains("already exists")), Is.True);
    }

    [Test]
    [Category("N")]
    public async Task BulkCreateIcdChaptersAsync_AllValid_PersistsAllAndReturnsResponses()
    {
        var request = new BulkCreateIcdChaptersRequest
        {
            Chapters = new List<CreateIcdChapterRequest>
            {
                new() { ChapterCode = "B00", ChapterName = "Herpes" },
                new() { ChapterCode = "A00", ChapterName = "Cholera" },
            },
        };

        _chapterRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IcdChapter>());

        var (succeeded, errors, data) = await _service.BulkCreateIcdChaptersAsync(request);

        Assert.That(succeeded, Is.True);
        Assert.That(data, Has.Count.EqualTo(2));
        Assert.That(data![0].ChapterCode, Is.EqualTo("A00")); // sorted
        _chapterRepoMock.Verify(r => r.Add(It.IsAny<IcdChapter>()), Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ UpdateIcdChapterAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task UpdateIcdChapterAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateIcdChapterAsync(
            Guid.Empty, new UpdateIcdChapterRequest { ChapterName = "x" });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Invalid ICD chapter id."));
    }

    [Test]
    [Category("A")]
    public async Task UpdateIcdChapterAsync_NullRequest_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateIcdChapterAsync(Guid.NewGuid(), null!);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Request body is required."));
    }

    [Test]
    [Category("B")]
    public async Task UpdateIcdChapterAsync_NoFieldsToUpdate_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateIcdChapterAsync(
            Guid.NewGuid(), new UpdateIcdChapterRequest());

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("No fields to update."));
    }

    [Test]
    [Category("A")]
    public async Task UpdateIcdChapterAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _chapterRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IcdChapter?)null);

        var (succeeded, notFound, errors, data) = await _service.UpdateIcdChapterAsync(
            id, new UpdateIcdChapterRequest { ChapterName = "New" });

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task UpdateIcdChapterAsync_NewCodeAlreadyExists_ReturnsError()
    {
        var id = Guid.NewGuid();
        _chapterRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IcdChapter { Id = id, ChapterCode = "A00", ChapterName = "Cholera", IsDeleted = false });

        _chapterRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<IcdChapter, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IcdChapter { Id = Guid.NewGuid(), ChapterCode = "B00" });

        var (succeeded, notFound, errors, data) = await _service.UpdateIcdChapterAsync(
            id, new UpdateIcdChapterRequest { ChapterCode = "B00" });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Chapter code already exists."));
    }

    [Test]
    [Category("N")]
    public async Task UpdateIcdChapterAsync_ScalarUpdateWithoutCodeChange_UpdatesFields()
    {
        var id = Guid.NewGuid();
        var existing = new IcdChapter { Id = id, ChapterCode = "A00", ChapterName = "Old", IsDeleted = false };
        _chapterRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var (succeeded, notFound, errors, data) = await _service.UpdateIcdChapterAsync(
            id, new UpdateIcdChapterRequest { ChapterName = "New name" });

        Assert.That(succeeded, Is.True);
        Assert.That(existing.ChapterName, Is.EqualTo("New name"));
        _chapterRepoMock.Verify(r => r.Update(existing), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Category("N")]
    public async Task UpdateIcdChapterAsync_CodeChanging_CascadesUpdateWithinTransaction()
    {
        var id = Guid.NewGuid();
        var existing = new IcdChapter { Id = id, ChapterCode = "A00", ChapterName = "Cholera", IsDeleted = false };
        _chapterRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _chapterRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<IcdChapter, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IcdChapter?)null);

        var departmentIds = new List<Guid> { Guid.NewGuid() };
        _departmentRepoMock.Setup(r => r.DetachChapterCodeAsync("A00", It.IsAny<CancellationToken>()))
            .ReturnsAsync(departmentIds);

        var (succeeded, notFound, errors, data) = await _service.UpdateIcdChapterAsync(
            id, new UpdateIcdChapterRequest { ChapterCode = "C00" });

        Assert.That(succeeded, Is.True);
        Assert.That(existing.ChapterCode, Is.EqualTo("C00"));
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _questionRepoMock.Verify(r => r.UpdateChapterCodeByChapterIdAsync(id, "C00", It.IsAny<CancellationToken>()), Times.Once);
        _chapterRepoMock.Verify(r => r.UpdateChapterCodeByIdAsync(id, "C00", It.IsAny<CancellationToken>()), Times.Once);
        _departmentRepoMock.Verify(r => r.AttachChapterCodeAsync(departmentIds, "C00", It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [Category("A")]
    public async Task UpdateIcdChapterAsync_CascadeThrows_RollsBackAndRethrows()
    {
        var id = Guid.NewGuid();
        var existing = new IcdChapter { Id = id, ChapterCode = "A00", ChapterName = "Cholera", IsDeleted = false };
        _chapterRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _chapterRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<IcdChapter, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IcdChapter?)null);

        _departmentRepoMock.Setup(r => r.DetachChapterCodeAsync("A00", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db error"));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.UpdateIcdChapterAsync(id, new UpdateIcdChapterRequest { ChapterCode = "C00" }));

        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ SoftDeleteIcdChapterAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task SoftDeleteIcdChapterAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors) = await _service.SoftDeleteIcdChapterAsync(Guid.Empty);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Invalid ICD chapter id."));
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteIcdChapterAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _chapterRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IcdChapter?)null);

        var (succeeded, notFound, errors) = await _service.SoftDeleteIcdChapterAsync(id);

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("N")]
    public async Task SoftDeleteIcdChapterAsync_ValidId_SoftDeletesChapter()
    {
        var id = Guid.NewGuid();
        var existing = new IcdChapter { Id = id, IsDeleted = false };
        _chapterRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var (succeeded, notFound, errors) = await _service.SoftDeleteIcdChapterAsync(id);

        Assert.That(succeeded, Is.True);
        Assert.That(existing.IsDeleted, Is.True);
        Assert.That(existing.DeletedAt, Is.Not.Null);
        _chapterRepoMock.Verify(r => r.Update(existing), Times.Once);
    }
}
