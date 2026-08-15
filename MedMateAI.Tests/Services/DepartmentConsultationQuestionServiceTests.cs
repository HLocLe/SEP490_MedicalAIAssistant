using System.Linq.Expressions;
using AutoMapper;
using MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Requests;
using MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Responses;
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
public class DepartmentConsultationQuestionServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IGenericRepository<DepartmentConsultationQuestion>> _questionsMock = null!;
    private Mock<IMedicalDepartmentRepository> _departmentRepositoryMock = null!;
    private Mock<IMapper> _mapperMock = null!;
    private DepartmentConsultationQuestionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _questionsMock = new Mock<IGenericRepository<DepartmentConsultationQuestion>>();
        _departmentRepositoryMock = new Mock<IMedicalDepartmentRepository>();
        _mapperMock = new Mock<IMapper>();

        _unitOfWorkMock.Setup(u => u.DepartmentConsultationQuestions).Returns(_questionsMock.Object);
        _unitOfWorkMock.Setup(u => u.MedicalDepartments).Returns(_departmentRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _mapperMock.Setup(m => m.Map<DepartmentConsultationQuestionResponse>(It.IsAny<DepartmentConsultationQuestion>()))
            .Returns((DepartmentConsultationQuestion src) => new DepartmentConsultationQuestionResponse
            {
                Id = src.Id,
                DepartmentId = src.DepartmentId,
                Category = src.Category,
                QuestionText = src.QuestionText,
                SortOrder = src.SortOrder,
                IsActive = src.IsActive,
                CreatedAt = src.CreatedAt,
                UpdatedAt = src.UpdatedAt,
            });

        _service = new DepartmentConsultationQuestionService(_unitOfWorkMock.Object, _mapperMock.Object);

        _questionsMock.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<DepartmentConsultationQuestion, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DepartmentConsultationQuestion?)null);
    }

    [Test]
    public async Task ListAsync_ReturnsMappedPagedResponse()
    {
        var items = new List<DepartmentConsultationQuestion> { MakeQuestion(text: "How long have symptoms lasted?") };
        _questionsMock.Setup(repository => repository.GetPagedAsync(
                1, 10,
                It.IsAny<Expression<Func<DepartmentConsultationQuestion, bool>>>(),
                It.IsAny<Func<IQueryable<DepartmentConsultationQuestion>, IOrderedQueryable<DepartmentConsultationQuestion>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<DepartmentConsultationQuestion>
            {
                PageNumber = 1,
                PageSize = 10,
                TotalCount = 1,
                TotalPages = 1,
                Items = items,
            });

        var result = await _service.ListAsync(1, 10, cancellationToken: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items[0].QuestionText, Is.EqualTo("How long have symptoms lasted?"));
        });
    }

    [Test]
    public async Task ListAsync_FiltersByDepartmentCategoryIsActiveAndSearch()
    {
        Expression<Func<DepartmentConsultationQuestion, bool>>? capturedPredicate = null;
        _questionsMock.Setup(repository => repository.GetPagedAsync(
                1, 10,
                It.IsAny<Expression<Func<DepartmentConsultationQuestion, bool>>>(),
                It.IsAny<Func<IQueryable<DepartmentConsultationQuestion>, IOrderedQueryable<DepartmentConsultationQuestion>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, int, Expression<Func<DepartmentConsultationQuestion, bool>>, Func<IQueryable<DepartmentConsultationQuestion>, IOrderedQueryable<DepartmentConsultationQuestion>>?, bool, CancellationToken>(
                (_, _, predicate, _, _, _) => capturedPredicate = predicate)
            .ReturnsAsync(new PagedResult<DepartmentConsultationQuestion> { Items = Array.Empty<DepartmentConsultationQuestion>() });

        var departmentId = Guid.NewGuid();

        await _service.ListAsync(1, 10, departmentId, ConsultationQuestionCategory.Tests, "fever", isActive: true);

        var compiled = capturedPredicate!.Compile();
        var matching = MakeQuestion(departmentId: departmentId, category: ConsultationQuestionCategory.Tests, text: "Any Fever recently?", isActive: true);
        var wrongCategory = MakeQuestion(departmentId: departmentId, category: ConsultationQuestionCategory.Diagnosis, text: "Any fever recently?", isActive: true);
        var wrongActive = MakeQuestion(departmentId: departmentId, category: ConsultationQuestionCategory.Tests, text: "Any fever recently?", isActive: false);
        var noMatch = MakeQuestion(departmentId: departmentId, category: ConsultationQuestionCategory.Tests, text: "Unrelated", isActive: true);

        Assert.Multiple(() =>
        {
            Assert.That(compiled(matching), Is.True);
            Assert.That(compiled(wrongCategory), Is.False);
            Assert.That(compiled(wrongActive), Is.False);
            Assert.That(compiled(noMatch), Is.False);
        });
    }

    [Test]
    public async Task GetByIdAsync_EmptyId_ReturnsNullWithoutCallingRepository()
    {
        var result = await _service.GetByIdAsync(Guid.Empty, CancellationToken.None);

        Assert.That(result, Is.Null);
        _questionsMock.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetByIdAsync_DeletedEntity_ReturnsNull()
    {
        var entity = MakeQuestion();
        entity.IsDeleted = true;
        _questionsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _service.GetByIdAsync(entity.Id, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByIdAsync_Found_ReturnsMappedResponse()
    {
        var entity = MakeQuestion(text: "found");
        _questionsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _service.GetByIdAsync(entity.Id, CancellationToken.None);

        Assert.That(result?.QuestionText, Is.EqualTo("found"));
    }

    [Test]
    public async Task CreateAsync_NullRequest_ReturnsValidationError()
    {
        var (succeeded, errors, data) = await _service.CreateAsync(null!, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(errors, Is.Not.Empty);
            Assert.That(data, Is.Null);
        });
    }

    [Test]
    public async Task CreateAsync_DepartmentIdEmpty_ReturnsValidationError()
    {
        var request = MakeCreateRequest(departmentId: Guid.Empty);

        var (succeeded, errors, _) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("khoa là bắt buộc"));
    }

    [Test]
    public async Task CreateAsync_DepartmentNotFound_ReturnsValidationError()
    {
        var departmentId = Guid.NewGuid();
        _departmentRepositoryMock.Setup(repository => repository.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalDepartment?)null);
        var request = MakeCreateRequest(departmentId: departmentId);

        var (succeeded, errors, _) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("Không tìm thấy khoa"));
    }

    [Test]
    public async Task CreateAsync_InvalidCategory_ReturnsValidationError()
    {
        var request = MakeCreateRequest();
        request.Category = (ConsultationQuestionCategory)999;
        SetupDepartmentExists(request.DepartmentId);

        var (succeeded, errors, _) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("Category"));
    }

    [Test]
    public async Task CreateAsync_SortOrderZero_ReturnsValidationError()
    {
        var request = MakeCreateRequest(sortOrder: 0);
        SetupDepartmentExists(request.DepartmentId);

        var (succeeded, errors, _) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("SortOrder"));
    }

    [Test]
    public async Task CreateAsync_BlankQuestionText_ReturnsValidationError()
    {
        var request = MakeCreateRequest(text: "   ");
        SetupDepartmentExists(request.DepartmentId);

        var (succeeded, errors, _) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("QuestionText là bắt buộc"));
    }

    [Test]
    public async Task CreateAsync_QuestionTextTooLong_ReturnsValidationError()
    {
        var request = MakeCreateRequest(text: new string('a', 1001));
        SetupDepartmentExists(request.DepartmentId);

        var (succeeded, errors, _) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("1000"));
    }

    [Test]
    public async Task CreateAsync_DuplicateQuestionForDepartment_ReturnsValidationError()
    {
        var request = MakeCreateRequest(text: "Existing question");
        SetupDepartmentExists(request.DepartmentId);
        _questionsMock.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<DepartmentConsultationQuestion, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeQuestion(departmentId: request.DepartmentId, text: "existing question"));

        var (succeeded, errors, _) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("đã tồn tại"));
    }

    [Test]
    public async Task CreateAsync_ValidRequest_AddsEntityAndReturnsMappedResponse()
    {
        var request = MakeCreateRequest(text: "  new question  ");
        SetupDepartmentExists(request.DepartmentId);

        var (succeeded, errors, data) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True);
            Assert.That(errors, Is.Empty);
            Assert.That(data?.QuestionText, Is.EqualTo("new question"));
        });
        _questionsMock.Verify(repository => repository.Add(It.IsAny<DepartmentConsultationQuestion>()), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task BulkCreateAsync_NullOrEmptyItems_ReturnsValidationError()
    {
        var (succeeded, errors, data) = await _service.BulkCreateAsync(
            new BulkCreateDepartmentConsultationQuestionsRequest { Questions = new() }, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Is.Not.Empty);
        Assert.That(data, Is.Null);
    }

    [Test]
    public async Task BulkCreateAsync_ItemFailsFieldValidation_ReturnsPrefixedErrorWithoutSaving()
    {
        var request = new BulkCreateDepartmentConsultationQuestionsRequest
        {
            Questions = new List<CreateDepartmentConsultationQuestionRequest> { MakeCreateRequest(departmentId: Guid.Empty) },
        };

        var (succeeded, errors, data) = await _service.BulkCreateAsync(request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(errors, Has.Some.Contains("Questions[0]"));
            Assert.That(data, Is.Null);
        });
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task BulkCreateAsync_DuplicateWithinRequest_ReturnsValidationError()
    {
        var departmentId = Guid.NewGuid();
        SetupDepartmentExists(departmentId);
        var request = new BulkCreateDepartmentConsultationQuestionsRequest
        {
            Questions = new List<CreateDepartmentConsultationQuestionRequest>
            {
                MakeCreateRequest(departmentId: departmentId, text: "Same question"),
                MakeCreateRequest(departmentId: departmentId, text: "same question"),
            },
        };

        var (succeeded, errors, _) = await _service.BulkCreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("Questions[1]"));
    }

    [Test]
    public async Task BulkCreateAsync_ExistingDbDuplicate_ReturnsValidationError()
    {
        var departmentId = Guid.NewGuid();
        SetupDepartmentExists(departmentId);
        var request = new BulkCreateDepartmentConsultationQuestionsRequest
        {
            Questions = new List<CreateDepartmentConsultationQuestionRequest>
            {
                MakeCreateRequest(departmentId: departmentId, text: "Already exists"),
            },
        };
        _questionsMock.Setup(repository => repository.GetAllAsync(
                It.IsAny<Expression<Func<DepartmentConsultationQuestion, bool>>>(),
                It.IsAny<Func<IQueryable<DepartmentConsultationQuestion>, IOrderedQueryable<DepartmentConsultationQuestion>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeQuestion(departmentId: departmentId, text: "already exists") });

        var (succeeded, errors, _) = await _service.BulkCreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("đã tồn tại"));
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task BulkCreateAsync_ValidQuestions_AddsAllAndSavesOnce()
    {
        var departmentId = Guid.NewGuid();
        SetupDepartmentExists(departmentId);
        _questionsMock.Setup(repository => repository.GetAllAsync(
                It.IsAny<Expression<Func<DepartmentConsultationQuestion, bool>>>(),
                It.IsAny<Func<IQueryable<DepartmentConsultationQuestion>, IOrderedQueryable<DepartmentConsultationQuestion>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DepartmentConsultationQuestion>());
        var request = new BulkCreateDepartmentConsultationQuestionsRequest
        {
            Questions = new List<CreateDepartmentConsultationQuestionRequest>
            {
                MakeCreateRequest(departmentId: departmentId, text: "Question one"),
                MakeCreateRequest(departmentId: departmentId, text: "Question two"),
            },
        };

        var (succeeded, errors, data) = await _service.BulkCreateAsync(request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True);
            Assert.That(errors, Is.Empty);
            Assert.That(data, Has.Count.EqualTo(2));
        });
        _questionsMock.Verify(repository => repository.Add(It.IsAny<DepartmentConsultationQuestion>()), Times.Exactly(2));
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_EmptyId_ReturnsValidationErrorNotNotFound()
    {
        var (succeeded, notFound, errors, _) = await _service.UpdateAsync(Guid.Empty, new UpdateDepartmentConsultationQuestionRequest(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.False);
            Assert.That(errors, Is.Not.Empty);
        });
    }

    [Test]
    public async Task UpdateAsync_NullRequest_ReturnsValidationError()
    {
        var (succeeded, notFound, errors, _) = await _service.UpdateAsync(Guid.NewGuid(), null!, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.False);
            Assert.That(errors, Is.Not.Empty);
        });
    }

    [Test]
    public async Task UpdateAsync_EntityNotFoundOrDeleted_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _questionsMock.Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DepartmentConsultationQuestion?)null);

        var (succeeded, notFound, _, _) = await _service.UpdateAsync(id, MakeUpdateRequest(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.True);
        });
    }

    [Test]
    public async Task UpdateAsync_MissingSortOrder_ReturnsValidationError()
    {
        var entity = MakeQuestion();
        _questionsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var (succeeded, notFound, errors, _) = await _service.UpdateAsync(
            entity.Id, new UpdateDepartmentConsultationQuestionRequest(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.False);
            Assert.That(errors, Has.Some.Contains("SortOrder"));
        });
    }

    [Test]
    public async Task UpdateAsync_DepartmentIdEmpty_ReturnsValidationError()
    {
        var entity = MakeQuestion();
        _questionsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        var request = MakeUpdateRequest();
        request.DepartmentId = Guid.Empty;

        var (succeeded, _, errors, _) = await _service.UpdateAsync(entity.Id, request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("Id khoa không hợp lệ"));
    }

    [Test]
    public async Task UpdateAsync_DepartmentNotFound_ReturnsValidationError()
    {
        var entity = MakeQuestion();
        var newDepartmentId = Guid.NewGuid();
        _questionsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _departmentRepositoryMock.Setup(repository => repository.GetByIdAsync(newDepartmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalDepartment?)null);
        var request = MakeUpdateRequest();
        request.DepartmentId = newDepartmentId;

        var (succeeded, _, errors, _) = await _service.UpdateAsync(entity.Id, request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("Không tìm thấy khoa"));
    }

    [Test]
    public async Task UpdateAsync_InvalidCategory_ReturnsValidationError()
    {
        var entity = MakeQuestion();
        _questionsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        var request = MakeUpdateRequest();
        request.Category = (ConsultationQuestionCategory)999;

        var (succeeded, _, errors, _) = await _service.UpdateAsync(entity.Id, request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("Category"));
    }

    [Test]
    public async Task UpdateAsync_BlankQuestionText_ReturnsValidationError()
    {
        var entity = MakeQuestion();
        _questionsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        var request = MakeUpdateRequest();
        request.QuestionText = "   ";

        var (succeeded, _, errors, _) = await _service.UpdateAsync(entity.Id, request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("không được để trống"));
    }

    [Test]
    public async Task UpdateAsync_DuplicateQuestionForDepartment_ReturnsValidationError()
    {
        var entity = MakeQuestion();
        _questionsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _questionsMock.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<DepartmentConsultationQuestion, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeQuestion());

        var (succeeded, notFound, errors, _) = await _service.UpdateAsync(entity.Id, MakeUpdateRequest(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.False);
            Assert.That(errors, Has.Some.Contains("đã tồn tại"));
        });
    }

    [Test]
    public async Task UpdateAsync_ValidRequest_UpdatesEntityAndSaves()
    {
        var entity = MakeQuestion(text: "old text", isActive: false);
        var newDepartmentId = Guid.NewGuid();
        _questionsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        SetupDepartmentExists(newDepartmentId);
        var request = new UpdateDepartmentConsultationQuestionRequest
        {
            DepartmentId = newDepartmentId,
            Category = ConsultationQuestionCategory.FollowUp,
            QuestionText = "  updated text  ",
            SortOrder = 5,
            IsActive = true,
        };

        var (succeeded, notFound, errors, data) = await _service.UpdateAsync(entity.Id, request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True);
            Assert.That(notFound, Is.False);
            Assert.That(errors, Is.Empty);
            Assert.That(data?.QuestionText, Is.EqualTo("updated text"));
            Assert.That(entity.DepartmentId, Is.EqualTo(newDepartmentId));
            Assert.That(entity.Category, Is.EqualTo(ConsultationQuestionCategory.FollowUp));
            Assert.That(entity.SortOrder, Is.EqualTo(5));
            Assert.That(entity.IsActive, Is.True);
        });
        _questionsMock.Verify(repository => repository.Update(entity), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SoftDeleteAsync_EmptyId_ReturnsValidationErrorNotNotFound()
    {
        var (succeeded, notFound, errors) = await _service.SoftDeleteAsync(Guid.Empty, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.False);
            Assert.That(errors, Is.Not.Empty);
        });
    }

    [Test]
    public async Task SoftDeleteAsync_EntityNotFoundOrDeleted_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _questionsMock.Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DepartmentConsultationQuestion?)null);

        var (succeeded, notFound, _) = await _service.SoftDeleteAsync(id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.True);
        });
    }

    [Test]
    public async Task SoftDeleteAsync_ValidEntity_MarksDeletedAndInactiveThenSaves()
    {
        var entity = MakeQuestion(isActive: true);
        _questionsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var (succeeded, notFound, errors) = await _service.SoftDeleteAsync(entity.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True);
            Assert.That(notFound, Is.False);
            Assert.That(errors, Is.Empty);
            Assert.That(entity.IsDeleted, Is.True);
            Assert.That(entity.IsActive, Is.False);
            Assert.That(entity.DeletedAt, Is.Not.Null);
        });
        _questionsMock.Verify(repository => repository.Update(entity), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupDepartmentExists(Guid departmentId)
    {
        _departmentRepositoryMock.Setup(repository => repository.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalDepartment { Id = departmentId });
    }

    private static CreateDepartmentConsultationQuestionRequest MakeCreateRequest(
        Guid? departmentId = null,
        ConsultationQuestionCategory category = ConsultationQuestionCategory.Diagnosis,
        string text = "question text",
        int sortOrder = 1) =>
        new()
        {
            DepartmentId = departmentId ?? Guid.NewGuid(),
            Category = category,
            QuestionText = text,
            SortOrder = sortOrder,
            IsActive = true,
        };

    private static UpdateDepartmentConsultationQuestionRequest MakeUpdateRequest() =>
        new()
        {
            SortOrder = 3,
        };

    private static DepartmentConsultationQuestion MakeQuestion(
        Guid? departmentId = null,
        ConsultationQuestionCategory category = ConsultationQuestionCategory.Diagnosis,
        string text = "question text",
        bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            DepartmentId = departmentId ?? Guid.NewGuid(),
            Category = category,
            QuestionText = text,
            SortOrder = 1,
            IsActive = isActive,
        };
}
