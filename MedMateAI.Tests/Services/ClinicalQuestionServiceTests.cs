using AutoMapper;
using MedMateAI.Application.DTOs.ClinicalQuestions.Requests;
using MedMateAI.Application.DTOs.ClinicalQuestions.Responses;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class ClinicalQuestionServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IClinicalQuestionRepository> _questionRepoMock = null!;
    private Mock<IIcdChapterRepository> _chapterRepoMock = null!;
    private Mock<IMapper> _mapperMock = null!;
    private ClinicalQuestionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _questionRepoMock = new Mock<IClinicalQuestionRepository>();
        _chapterRepoMock = new Mock<IIcdChapterRepository>();
        _mapperMock = new Mock<IMapper>();

        _unitOfWorkMock.Setup(u => u.ClinicalQuestions).Returns(_questionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.IcdChapters).Returns(_chapterRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _mapperMock.Setup(m => m.Map<ClinicalQuestionResponse>(It.IsAny<ClinicalQuestion>()))
            .Returns((ClinicalQuestion src) => new ClinicalQuestionResponse
            {
                Id = src.Id,
                ChapterId = src.ChapterId,
                ChapterCode = src.ChapterCode,
                QuestionVi = src.QuestionVi,
                EnglishPrefix = src.EnglishPrefix,
                SortOrder = src.SortOrder,
                Answers = src.Answers,
                CreatedAt = src.CreatedAt,
                UpdatedAt = src.UpdatedAt,
            });

        _service = new ClinicalQuestionService(_unitOfWorkMock.Object, _mapperMock.Object);
    }

    // â”€â”€ ListClinicalQuestionsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task ListClinicalQuestionsAsync_ValidRequest_ReturnsPagedResponse()
    {
        var question = new ClinicalQuestion { Id = Guid.NewGuid(), QuestionVi = "Ä�au Ä‘áº§u?" };
        var pagedResult = new PagedResult<ClinicalQuestion>
        {
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPages = 1,
            Items = new List<ClinicalQuestion> { question },
        };

        _questionRepoMock.Setup(r => r.GetPagedAsync(
                1, 10,
                It.IsAny<System.Linq.Expressions.Expression<Func<ClinicalQuestion, bool>>>(),
                It.IsAny<Func<IQueryable<ClinicalQuestion>, IOrderedQueryable<ClinicalQuestion>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _service.ListClinicalQuestionsAsync(1, 10);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].QuestionVi, Is.EqualTo("Ä�au Ä‘áº§u?"));
    }

    // â”€â”€ GetClinicalQuestionByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetClinicalQuestionByIdAsync_EmptyId_ReturnsNull()
    {
        Assert.That(await _service.GetClinicalQuestionByIdAsync(Guid.Empty), Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetClinicalQuestionByIdAsync_NotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClinicalQuestion?)null);

        Assert.That(await _service.GetClinicalQuestionByIdAsync(id), Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetClinicalQuestionByIdAsync_Deleted_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClinicalQuestion { Id = id, IsDeleted = true });

        Assert.That(await _service.GetClinicalQuestionByIdAsync(id), Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetClinicalQuestionByIdAsync_Found_ReturnsResponse()
    {
        var id = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClinicalQuestion { Id = id, QuestionVi = "Sá»‘t?", IsDeleted = false });

        var result = await _service.GetClinicalQuestionByIdAsync(id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.QuestionVi, Is.EqualTo("Sá»‘t?"));
    }

    // â”€â”€ CreateClinicalQuestionAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("A")]
    public async Task CreateClinicalQuestionAsync_NullRequest_ReturnsFailed()
    {
        var (succeeded, errors, data) = await _service.CreateClinicalQuestionAsync(null!);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Request body là bắt buộc"));
    }

    [Test]
    [Category("B")]
    public async Task CreateClinicalQuestionAsync_EmptyChapterIdAndQuestion_ReturnsErrors()
    {
        var request = new CreateClinicalQuestionRequest { ChapterId = Guid.Empty, QuestionVi = " " };

        var (succeeded, errors, data) = await _service.CreateClinicalQuestionAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("ChapterId là bắt buộc"));
        Assert.That(errors, Contains.Item("Nội dung câu hỏi là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task CreateClinicalQuestionAsync_ChapterNotFound_ReturnsError()
    {
        var chapterId = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.IcdChapterExistsAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new CreateClinicalQuestionRequest { ChapterId = chapterId, QuestionVi = "Ho?" };

        var (succeeded, errors, data) = await _service.CreateClinicalQuestionAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Không tìm thấy ICD chapter"));
    }

    [Test]
    [Category("A")]
    public async Task CreateClinicalQuestionAsync_InvalidAnswers_ReturnsErrors()
    {
        var chapterId = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.IcdChapterExistsAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateClinicalQuestionRequest
        {
            ChapterId = chapterId,
            QuestionVi = "Ho?",
            Answers = new Dictionary<string, string> { { " ", "" } },
        };

        var (succeeded, errors, data) = await _service.CreateClinicalQuestionAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors.Count(), Is.EqualTo(2));
    }

    [Test]
    [Category("N")]
    public async Task CreateClinicalQuestionAsync_ValidRequest_CreatesAndReturnsResponse()
    {
        var chapterId = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.IcdChapterExistsAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IcdChapter { Id = chapterId, ChapterCode = "A00", IsDeleted = false });

        var request = new CreateClinicalQuestionRequest
        {
            ChapterId = chapterId,
            QuestionVi = " Ho khan? ",
            EnglishPrefix = " cough ",
            SortOrder = 1,
            Answers = new Dictionary<string, string> { { " CÃ³ ", " Yes " } },
        };

        var (succeeded, errors, data) = await _service.CreateClinicalQuestionAsync(request);

        Assert.That(succeeded, Is.True);
        Assert.That(data, Is.Not.Null);
        Assert.That(data!.QuestionVi, Is.EqualTo("Ho khan?"));
        Assert.That(data.ChapterCode, Is.EqualTo("A00"));
        _questionRepoMock.Verify(r => r.Add(It.IsAny<ClinicalQuestion>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ BulkCreateClinicalQuestionsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task BulkCreateClinicalQuestionsAsync_EmptyQuestions_ReturnsError()
    {
        var request = new BulkCreateClinicalQuestionsRequest { Questions = new List<CreateClinicalQuestionRequest>() };

        var (succeeded, errors, data) = await _service.BulkCreateClinicalQuestionsAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Cần ít nhất một câu hỏi"));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateClinicalQuestionsAsync_NullItem_ReturnsIndexedError()
    {
        var request = new BulkCreateClinicalQuestionsRequest
        {
            Questions = new List<CreateClinicalQuestionRequest> { null! },
        };

        var (succeeded, errors, data) = await _service.BulkCreateClinicalQuestionsAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Questions[0]: Mục câu hỏi là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateClinicalQuestionsAsync_OneInvalidItem_DoesNotPersistAny()
    {
        var chapterId = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.IcdChapterExistsAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new BulkCreateClinicalQuestionsRequest
        {
            Questions = new List<CreateClinicalQuestionRequest>
            {
                new() { ChapterId = chapterId, QuestionVi = "Sá»‘t?" },
                new() { ChapterId = Guid.Empty, QuestionVi = " " },
            },
        };

        var (succeeded, errors, data) = await _service.BulkCreateClinicalQuestionsAsync(request);

        Assert.That(succeeded, Is.False);
        Assert.That(errors.Any(e => e.StartsWith("Questions[1]:")), Is.True);
        _questionRepoMock.Verify(r => r.Add(It.IsAny<ClinicalQuestion>()), Times.Never);
    }

    [Test]
    [Category("N")]
    public async Task BulkCreateClinicalQuestionsAsync_AllValid_PersistsAllAndReturnsResponses()
    {
        var chapterId = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.IcdChapterExistsAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IcdChapter { Id = chapterId, ChapterCode = "B00", IsDeleted = false });

        var request = new BulkCreateClinicalQuestionsRequest
        {
            Questions = new List<CreateClinicalQuestionRequest>
            {
                new() { ChapterId = chapterId, QuestionVi = "Sá»‘t?" },
                new() { ChapterId = chapterId, QuestionVi = "Ho?" },
            },
        };

        var (succeeded, errors, data) = await _service.BulkCreateClinicalQuestionsAsync(request);

        Assert.That(succeeded, Is.True);
        Assert.That(data, Has.Count.EqualTo(2));
        _questionRepoMock.Verify(r => r.Add(It.IsAny<ClinicalQuestion>()), Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ UpdateClinicalQuestionAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task UpdateClinicalQuestionAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateClinicalQuestionAsync(
            Guid.Empty, new UpdateClinicalQuestionRequest { QuestionVi = "x" });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Id câu hỏi không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateClinicalQuestionAsync_NullRequest_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateClinicalQuestionAsync(Guid.NewGuid(), null!);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Request body là bắt buộc"));
    }

    [Test]
    [Category("B")]
    public async Task UpdateClinicalQuestionAsync_NoFieldsToUpdate_ReturnsError()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateClinicalQuestionAsync(
            Guid.NewGuid(), new UpdateClinicalQuestionRequest());

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Không có trường nào để cập nhật"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateClinicalQuestionAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClinicalQuestion?)null);

        var (succeeded, notFound, errors, data) = await _service.UpdateClinicalQuestionAsync(
            id, new UpdateClinicalQuestionRequest { QuestionVi = "x" });

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("B")]
    public async Task UpdateClinicalQuestionAsync_NewChapterIdEmpty_ReturnsError()
    {
        var id = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClinicalQuestion { Id = id, IsDeleted = false });

        var (succeeded, notFound, errors, data) = await _service.UpdateClinicalQuestionAsync(
            id, new UpdateClinicalQuestionRequest { ChapterId = Guid.Empty });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("ChapterId không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateClinicalQuestionAsync_NewChapterNotFound_ReturnsError()
    {
        var id = Guid.NewGuid();
        var newChapterId = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClinicalQuestion { Id = id, IsDeleted = false });
        _chapterRepoMock.Setup(r => r.GetByIdAsync(newChapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IcdChapter?)null);

        var (succeeded, notFound, errors, data) = await _service.UpdateClinicalQuestionAsync(
            id, new UpdateClinicalQuestionRequest { ChapterId = newChapterId });

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Không tìm thấy ICD chapter"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateClinicalQuestionAsync_InvalidAnswers_ReturnsErrors()
    {
        var id = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClinicalQuestion { Id = id, IsDeleted = false });

        var (succeeded, notFound, errors, data) = await _service.UpdateClinicalQuestionAsync(
            id, new UpdateClinicalQuestionRequest { Answers = new Dictionary<string, string> { { " ", "" } } });

        Assert.That(succeeded, Is.False);
        Assert.That(errors.Count(), Is.EqualTo(2));
    }

    [Test]
    [Category("N")]
    public async Task UpdateClinicalQuestionAsync_ValidRequest_UpdatesAndReturnsResponse()
    {
        var id = Guid.NewGuid();
        var existing = new ClinicalQuestion { Id = id, QuestionVi = "Old", IsDeleted = false };
        _questionRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new UpdateClinicalQuestionRequest { QuestionVi = "New question", SortOrder = 5 };

        var (succeeded, notFound, errors, data) = await _service.UpdateClinicalQuestionAsync(id, request);

        Assert.That(succeeded, Is.True);
        Assert.That(existing.QuestionVi, Is.EqualTo("New question"));
        Assert.That(existing.SortOrder, Is.EqualTo(5));
        _questionRepoMock.Verify(r => r.Update(existing), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // â”€â”€ SoftDeleteClinicalQuestionAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task SoftDeleteClinicalQuestionAsync_EmptyId_ReturnsError()
    {
        var (succeeded, notFound, errors) = await _service.SoftDeleteClinicalQuestionAsync(Guid.Empty);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Contains.Item("Id câu hỏi không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteClinicalQuestionAsync_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _questionRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClinicalQuestion?)null);

        var (succeeded, notFound, errors) = await _service.SoftDeleteClinicalQuestionAsync(id);

        Assert.That(succeeded, Is.False);
        Assert.That(notFound, Is.True);
    }

    [Test]
    [Category("N")]
    public async Task SoftDeleteClinicalQuestionAsync_ValidId_SoftDeletesQuestion()
    {
        var id = Guid.NewGuid();
        var existing = new ClinicalQuestion { Id = id, IsDeleted = false };
        _questionRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var (succeeded, notFound, errors) = await _service.SoftDeleteClinicalQuestionAsync(id);

        Assert.That(succeeded, Is.True);
        Assert.That(existing.IsDeleted, Is.True);
        Assert.That(existing.DeletedAt, Is.Not.Null);
        _questionRepoMock.Verify(r => r.Update(existing), Times.Once);
    }
}
