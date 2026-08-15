using System.Linq.Expressions;
using AutoMapper;
using MedMateAI.Application.DTOs.ChecklistItems.Requests;
using MedMateAI.Application.DTOs.ChecklistItems.Responses;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class ChecklistItemServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<IGenericRepository<ChecklistItem>> _checklistItemsMock = null!;
    private Mock<IMedicalDepartmentRepository> _departmentRepositoryMock = null!;
    private Mock<IMedicalFacilityRepository> _facilityRepositoryMock = null!;
    private Mock<IMapper> _mapperMock = null!;
    private ChecklistItemService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _checklistItemsMock = new Mock<IGenericRepository<ChecklistItem>>();
        _departmentRepositoryMock = new Mock<IMedicalDepartmentRepository>();
        _facilityRepositoryMock = new Mock<IMedicalFacilityRepository>();
        _mapperMock = new Mock<IMapper>();

        _unitOfWorkMock.Setup(u => u.ChecklistItems).Returns(_checklistItemsMock.Object);
        _unitOfWorkMock.Setup(u => u.MedicalDepartments).Returns(_departmentRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.MedicalFacilities).Returns(_facilityRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _mapperMock.Setup(m => m.Map<ChecklistItemResponse>(It.IsAny<ChecklistItem>()))
            .Returns((ChecklistItem src) => new ChecklistItemResponse
            {
                Id = src.Id,
                Content = src.Content,
                DepartmentId = src.DepartmentId,
                FacilityId = src.FacilityId,
                IsMandatory = src.IsMandatory,
                CreatedAt = src.CreatedAt,
                UpdatedAt = src.UpdatedAt,
            });

        _service = new ChecklistItemService(_unitOfWorkMock.Object, _mapperMock.Object);
    }

    [Test]
    public async Task ListAsync_ReturnsMappedPagedResponse()
    {
        var items = new List<ChecklistItem> { MakeItem(content: "wash hands") };
        _checklistItemsMock.Setup(repository => repository.GetPagedAsync(
                1, 20,
                It.IsAny<Expression<Func<ChecklistItem, bool>>>(),
                It.IsAny<Func<IQueryable<ChecklistItem>, IOrderedQueryable<ChecklistItem>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ChecklistItem>
            {
                PageNumber = 1,
                PageSize = 20,
                TotalCount = 1,
                TotalPages = 1,
                Items = items,
            });

        var result = await _service.ListAsync(1, 20, cancellationToken: CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Content, Is.EqualTo("wash hands"));
        });
    }

    [Test]
    public async Task ListAsync_FiltersByDepartmentFacilityMandatoryAndSearchTerm()
    {
        Expression<Func<ChecklistItem, bool>>? capturedPredicate = null;
        _checklistItemsMock.Setup(repository => repository.GetPagedAsync(
                1, 20,
                It.IsAny<Expression<Func<ChecklistItem, bool>>>(),
                It.IsAny<Func<IQueryable<ChecklistItem>, IOrderedQueryable<ChecklistItem>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, int, Expression<Func<ChecklistItem, bool>>, Func<IQueryable<ChecklistItem>, IOrderedQueryable<ChecklistItem>>?, bool, CancellationToken>(
                (_, _, predicate, _, _, _) => capturedPredicate = predicate)
            .ReturnsAsync(new PagedResult<ChecklistItem> { Items = Array.Empty<ChecklistItem>() });

        var departmentId = Guid.NewGuid();
        var facilityId = Guid.NewGuid();

        await _service.ListAsync(1, 20, departmentId, facilityId, isMandatory: true, search: "  Hands  ");

        Assert.That(capturedPredicate, Is.Not.Null);
        var compiled = capturedPredicate!.Compile();

        var matching = MakeItem(content: "wash Hands now", departmentId: departmentId, facilityId: facilityId, isMandatory: true);
        var wrongDepartment = MakeItem(content: "wash hands", departmentId: Guid.NewGuid(), facilityId: facilityId, isMandatory: true);
        var wrongMandatory = MakeItem(content: "wash hands", departmentId: departmentId, facilityId: facilityId, isMandatory: false);
        var noSearchMatch = MakeItem(content: "unrelated content", departmentId: departmentId, facilityId: facilityId, isMandatory: true);
        var deleted = MakeItem(content: "wash hands", departmentId: departmentId, facilityId: facilityId, isMandatory: true);
        deleted.IsDeleted = true;

        Assert.Multiple(() =>
        {
            Assert.That(compiled(matching), Is.True);
            Assert.That(compiled(wrongDepartment), Is.False);
            Assert.That(compiled(wrongMandatory), Is.False);
            Assert.That(compiled(noSearchMatch), Is.False);
            Assert.That(compiled(deleted), Is.False);
        });
    }

    [Test]
    public async Task GetByIdAsync_EmptyId_ReturnsNullWithoutCallingRepository()
    {
        var result = await _service.GetByIdAsync(Guid.Empty, CancellationToken.None);

        Assert.That(result, Is.Null);
        _checklistItemsMock.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _checklistItemsMock.Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChecklistItem?)null);

        var result = await _service.GetByIdAsync(id, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByIdAsync_SoftDeleted_ReturnsNull()
    {
        var item = MakeItem();
        item.IsDeleted = true;
        _checklistItemsMock.Setup(repository => repository.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var result = await _service.GetByIdAsync(item.Id, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByIdAsync_Found_ReturnsMappedResponse()
    {
        var item = MakeItem(content: "found item");
        _checklistItemsMock.Setup(repository => repository.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var result = await _service.GetByIdAsync(item.Id, CancellationToken.None);

        Assert.That(result?.Content, Is.EqualTo("found item"));
    }

    [Test]
    public async Task GetByDepartmentIdAsync_EmptyId_ReturnsEmptyWithoutCallingRepository()
    {
        var result = await _service.GetByDepartmentIdAsync(Guid.Empty, CancellationToken.None);

        Assert.That(result, Is.Empty);
        _checklistItemsMock.Verify(repository => repository.GetAllAsync(
            It.IsAny<Expression<Func<ChecklistItem, bool>>>(),
            It.IsAny<Func<IQueryable<ChecklistItem>, IOrderedQueryable<ChecklistItem>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetByDepartmentIdAsync_ValidId_ReturnsMappedList()
    {
        var departmentId = Guid.NewGuid();
        var items = new List<ChecklistItem> { MakeItem(departmentId: departmentId) };
        _checklistItemsMock.Setup(repository => repository.GetAllAsync(
                It.IsAny<Expression<Func<ChecklistItem, bool>>>(),
                It.IsAny<Func<IQueryable<ChecklistItem>, IOrderedQueryable<ChecklistItem>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _service.GetByDepartmentIdAsync(departmentId, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetByFacilityIdAsync_EmptyId_ReturnsEmptyWithoutCallingRepository()
    {
        var result = await _service.GetByFacilityIdAsync(Guid.Empty, CancellationToken.None);

        Assert.That(result, Is.Empty);
        _checklistItemsMock.Verify(repository => repository.GetAllAsync(
            It.IsAny<Expression<Func<ChecklistItem, bool>>>(),
            It.IsAny<Func<IQueryable<ChecklistItem>, IOrderedQueryable<ChecklistItem>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetByFacilityIdAsync_ValidId_ReturnsMappedList()
    {
        var facilityId = Guid.NewGuid();
        var items = new List<ChecklistItem> { MakeItem(facilityId: facilityId) };
        _checklistItemsMock.Setup(repository => repository.GetAllAsync(
                It.IsAny<Expression<Func<ChecklistItem, bool>>>(),
                It.IsAny<Func<IQueryable<ChecklistItem>, IOrderedQueryable<ChecklistItem>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _service.GetByFacilityIdAsync(facilityId, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
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
        _checklistItemsMock.Verify(repository => repository.Add(It.IsAny<ChecklistItem>()), Times.Never);
    }

    [Test]
    public async Task CreateAsync_BlankContent_ReturnsValidationError()
    {
        var request = new CreateChecklistItemRequest { Content = "  " };

        var (succeeded, errors, _) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("Nội dung"));
    }

    [Test]
    public async Task CreateAsync_ContentTooLong_ReturnsValidationError()
    {
        var request = new CreateChecklistItemRequest { Content = new string('a', 1001) };

        var (succeeded, errors, _) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("1000"));
    }

    [Test]
    public async Task CreateAsync_DepartmentNotFound_ReturnsValidationError()
    {
        var departmentId = Guid.NewGuid();
        _departmentRepositoryMock.Setup(repository => repository.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalDepartment?)null);
        var request = new CreateChecklistItemRequest { Content = "valid content", DepartmentId = departmentId };

        var (succeeded, errors, _) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("khoa"));
    }

    [Test]
    public async Task CreateAsync_FacilityNotFound_ReturnsValidationError()
    {
        var facilityId = Guid.NewGuid();
        _facilityRepositoryMock.Setup(repository => repository.GetByIdAsync(facilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MedicalFacility?)null);
        var request = new CreateChecklistItemRequest { Content = "valid content", FacilityId = facilityId };

        var (succeeded, errors, _) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("cơ sở"));
    }

    [Test]
    public async Task CreateAsync_ValidRequest_AddsEntityAndReturnsMappedResponse()
    {
        var request = new CreateChecklistItemRequest { Content = "  new item  ", IsMandatory = true };

        var (succeeded, errors, data) = await _service.CreateAsync(request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True);
            Assert.That(errors, Is.Empty);
            Assert.That(data?.Content, Is.EqualTo("new item"));
        });
        _checklistItemsMock.Verify(repository => repository.Add(It.Is<ChecklistItem>(item => item.Content == "new item")), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task BulkCreateAsync_NullOrEmptyItems_ReturnsValidationError()
    {
        var (succeeded, errors, data) = await _service.BulkCreateAsync(new BulkCreateChecklistItemsRequest { Items = new() }, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Is.Not.Empty);
        Assert.That(data, Is.Null);
    }

    [Test]
    public async Task BulkCreateAsync_ItemFailsValidation_ReturnsPrefixedErrorsWithoutSaving()
    {
        var request = new BulkCreateChecklistItemsRequest
        {
            Items = new List<CreateChecklistItemRequest> { new() { Content = "  " } },
        };

        var (succeeded, errors, data) = await _service.BulkCreateAsync(request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(errors, Has.Some.Contains("Items[0]"));
            Assert.That(data, Is.Null);
        });
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task BulkCreateAsync_DuplicateContentInRequest_ReturnsValidationError()
    {
        var request = new BulkCreateChecklistItemsRequest
        {
            Items = new List<CreateChecklistItemRequest>
            {
                new() { Content = "Wash Hands" },
                new() { Content = "wash hands" },
            },
        };

        var (succeeded, errors, _) = await _service.BulkCreateAsync(request, CancellationToken.None);

        Assert.That(succeeded, Is.False);
        Assert.That(errors, Has.Some.Contains("Items[1]"));
    }

    [Test]
    public async Task BulkCreateAsync_ValidItems_AddsAllAndSavesOnce()
    {
        var request = new BulkCreateChecklistItemsRequest
        {
            Items = new List<CreateChecklistItemRequest>
            {
                new() { Content = "item one" },
                new() { Content = "item two" },
            },
        };

        var (succeeded, errors, data) = await _service.BulkCreateAsync(request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True);
            Assert.That(errors, Is.Empty);
            Assert.That(data, Has.Count.EqualTo(2));
        });
        _checklistItemsMock.Verify(repository => repository.Add(It.IsAny<ChecklistItem>()), Times.Exactly(2));
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_EmptyId_ReturnsNotFound()
    {
        var (succeeded, notFound, errors, data) = await _service.UpdateAsync(Guid.Empty, new UpdateChecklistItemRequest(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.True);
            Assert.That(errors, Is.Empty);
            Assert.That(data, Is.Null);
        });
    }

    [Test]
    public async Task UpdateAsync_NullRequest_ReturnsValidationErrorNotNotFound()
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
        _checklistItemsMock.Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChecklistItem?)null);

        var (succeeded, notFound, _, _) = await _service.UpdateAsync(id, new UpdateChecklistItemRequest { Content = "x" }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.True);
        });
    }

    [Test]
    public async Task UpdateAsync_BlankContentOverride_ReturnsValidationError()
    {
        var entity = MakeItem(content: "existing");
        _checklistItemsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var (succeeded, notFound, errors, _) = await _service.UpdateAsync(entity.Id, new UpdateChecklistItemRequest { Content = "   " }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.False);
            Assert.That(errors, Is.Not.Empty);
        });
    }

    [Test]
    public async Task UpdateAsync_ValidRequest_UpdatesFieldsAndSaves()
    {
        var entity = MakeItem(content: "old content", isMandatory: false);
        var newDepartmentId = Guid.NewGuid();
        _checklistItemsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _departmentRepositoryMock.Setup(repository => repository.GetByIdAsync(newDepartmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalDepartment { Id = newDepartmentId });

        var (succeeded, notFound, errors, data) = await _service.UpdateAsync(
            entity.Id,
            new UpdateChecklistItemRequest { Content = "  new content  ", DepartmentId = newDepartmentId, IsMandatory = true },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True);
            Assert.That(notFound, Is.False);
            Assert.That(errors, Is.Empty);
            Assert.That(data?.Content, Is.EqualTo("new content"));
            Assert.That(entity.DepartmentId, Is.EqualTo(newDepartmentId));
            Assert.That(entity.IsMandatory, Is.True);
        });
        _checklistItemsMock.Verify(repository => repository.Update(entity), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_FieldsOmitted_PreservesExistingValues()
    {
        var existingDepartmentId = Guid.NewGuid();
        var entity = MakeItem(content: "keep me", departmentId: existingDepartmentId, isMandatory: true);
        _checklistItemsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _departmentRepositoryMock.Setup(repository => repository.GetByIdAsync(existingDepartmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalDepartment { Id = existingDepartmentId });

        var (succeeded, _, _, data) = await _service.UpdateAsync(entity.Id, new UpdateChecklistItemRequest(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True);
            Assert.That(data?.Content, Is.EqualTo("keep me"));
            Assert.That(entity.DepartmentId, Is.EqualTo(existingDepartmentId));
            Assert.That(entity.IsMandatory, Is.True);
        });
    }

    [Test]
    public async Task SoftDeleteAsync_EmptyId_ReturnsNotFound()
    {
        var (succeeded, notFound, errors) = await _service.SoftDeleteAsync(Guid.Empty, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.True);
            Assert.That(errors, Is.Empty);
        });
        _checklistItemsMock.Verify(repository => repository.Update(It.IsAny<ChecklistItem>()), Times.Never);
    }

    [Test]
    public async Task SoftDeleteAsync_EntityNotFoundOrAlreadyDeleted_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _checklistItemsMock.Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChecklistItem?)null);

        var (succeeded, notFound, _) = await _service.SoftDeleteAsync(id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(notFound, Is.True);
        });
    }

    [Test]
    public async Task SoftDeleteAsync_ValidEntity_MarksDeletedAndSaves()
    {
        var entity = MakeItem();
        _checklistItemsMock.Setup(repository => repository.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var (succeeded, notFound, errors) = await _service.SoftDeleteAsync(entity.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True);
            Assert.That(notFound, Is.False);
            Assert.That(errors, Is.Empty);
            Assert.That(entity.IsDeleted, Is.True);
            Assert.That(entity.DeletedAt, Is.Not.Null);
        });
        _checklistItemsMock.Verify(repository => repository.Update(entity), Times.Once);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ChecklistItem MakeItem(
        string content = "content",
        Guid? departmentId = null,
        Guid? facilityId = null,
        bool isMandatory = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Content = content,
            DepartmentId = departmentId,
            FacilityId = facilityId,
            IsMandatory = isMandatory,
        };
}
