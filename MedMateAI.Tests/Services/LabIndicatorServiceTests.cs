using System.Linq.Expressions;
using AutoMapper;
using MedMateAI.Application.DTOs.LabIndicators.Requests;
using MedMateAI.Application.DTOs.LabIndicators.Responses;
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
public class LabIndicatorServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<ILabIndicatorRepository> _indicatorRepoMock = null!;
    private Mock<IGenericRepository<LabIndicatorAlias>> _aliasRepoMock = null!;
    private Mock<IGenericRepository<LabIndicatorReferenceRange>> _rangeRepoMock = null!;
    private Mock<IGenericRepository<LabIndicatorAdviceCache>> _adviceRepoMock = null!;
    private Mock<IMapper> _mapperMock = null!;
    private LabIndicatorService _service = null!;

    private static readonly Guid IndicatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ChildId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherIndicatorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private LabIndicatorMaster _existingIndicator = null!;

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _indicatorRepoMock = new Mock<ILabIndicatorRepository>();
        _aliasRepoMock = new Mock<IGenericRepository<LabIndicatorAlias>>();
        _rangeRepoMock = new Mock<IGenericRepository<LabIndicatorReferenceRange>>();
        _adviceRepoMock = new Mock<IGenericRepository<LabIndicatorAdviceCache>>();
        _mapperMock = new Mock<IMapper>();

        _unitOfWorkMock.Setup(u => u.LabIndicators).Returns(_indicatorRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.LabIndicatorAliases).Returns(_aliasRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.LabIndicatorReferenceRanges).Returns(_rangeRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.LabIndicatorAdviceCaches).Returns(_adviceRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _existingIndicator = MakeIndicator();

        // Default happy-path lookups â€” individual tests override what they need.
        _indicatorRepoMock.Setup(r => r.GetByIdAsync(IndicatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _existingIndicator);
        _indicatorRepoMock.Setup(r => r.SymbolExistsAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        SetupAliasQuery(Array.Empty<LabIndicatorAlias>());
        SetupRangeQuery(Array.Empty<LabIndicatorReferenceRange>());
        SetupAdviceQuery(Array.Empty<LabIndicatorAdviceCache>());

        _aliasRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<LabIndicatorAlias, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabIndicatorAlias?)null);
        _adviceRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<LabIndicatorAdviceCache, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabIndicatorAdviceCache?)null);

        SetupMapper();

        _service = new LabIndicatorService(_unitOfWorkMock.Object, _mapperMock.Object);
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void SetupMapper()
    {
        _mapperMock.Setup(m => m.Map<LabIndicatorResponse>(It.IsAny<LabIndicatorMaster>()))
            .Returns((LabIndicatorMaster src) => new LabIndicatorResponse
            {
                IndicatorId = src.Id,
                Symbol = src.Symbol,
                FullName = src.FullName,
                Unit = src.Unit,
                MinReference = src.MinReference,
                MaxReference = src.MaxReference,
                Description = src.Description,
                Category = src.Category,
                IsActive = src.IsActive,
            });

        _mapperMock.Setup(m => m.Map<LabIndicatorDetailResponse>(It.IsAny<LabIndicatorMaster>()))
            .Returns((LabIndicatorMaster src) => new LabIndicatorDetailResponse
            {
                IndicatorId = src.Id,
                Symbol = src.Symbol,
            });

        _mapperMock.Setup(m => m.Map<LabIndicatorAliasResponse>(It.IsAny<LabIndicatorAlias>()))
            .Returns((LabIndicatorAlias src) => new LabIndicatorAliasResponse
            {
                AliasId = src.Id,
                IndicatorId = src.IndicatorId,
                AliasText = src.AliasText,
                Language = src.Language,
                IsPrimary = src.IsPrimary,
            });

        _mapperMock.Setup(m => m.Map<LabIndicatorReferenceRangeResponse>(It.IsAny<LabIndicatorReferenceRange>()))
            .Returns((LabIndicatorReferenceRange src) => new LabIndicatorReferenceRangeResponse
            {
                ReferenceRangeId = src.Id,
                IndicatorId = src.IndicatorId,
                Gender = src.Gender,
                AgeGroup = src.AgeGroup,
                ComparisonType = src.ComparisonType,
                MinValue = src.MinValue,
                MaxValue = src.MaxValue,
                Unit = src.Unit,
            });

        _mapperMock.Setup(m => m.Map<LabIndicatorAdviceCacheResponse>(It.IsAny<LabIndicatorAdviceCache>()))
            .Returns((LabIndicatorAdviceCache src) => new LabIndicatorAdviceCacheResponse
            {
                CacheId = src.Id,
                IndicatorId = src.IndicatorId,
                Status = src.Status,
                DisplayTitle = src.DisplayTitle,
                Summary = src.Summary,
                SeverityLevel = src.SeverityLevel,
            });
    }

    private void SetupAliasQuery(IReadOnlyList<LabIndicatorAlias> result) =>
        _aliasRepoMock.Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<LabIndicatorAlias, bool>>>(),
                It.IsAny<Func<IQueryable<LabIndicatorAlias>, IOrderedQueryable<LabIndicatorAlias>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void SetupRangeQuery(IReadOnlyList<LabIndicatorReferenceRange> result) =>
        _rangeRepoMock.Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<LabIndicatorReferenceRange, bool>>>(),
                It.IsAny<Func<IQueryable<LabIndicatorReferenceRange>, IOrderedQueryable<LabIndicatorReferenceRange>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void SetupAdviceQuery(IReadOnlyList<LabIndicatorAdviceCache> result) =>
        _adviceRepoMock.Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<LabIndicatorAdviceCache, bool>>>(),
                It.IsAny<Func<IQueryable<LabIndicatorAdviceCache>, IOrderedQueryable<LabIndicatorAdviceCache>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void SetupPagedIndicators(PagedResult<LabIndicatorMaster> paged) =>
        _indicatorRepoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<LabIndicatorMaster, bool>>>(),
                It.IsAny<Func<IQueryable<LabIndicatorMaster>, IOrderedQueryable<LabIndicatorMaster>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

    private static PagedResult<LabIndicatorMaster> EmptyPage() => new()
    {
        PageNumber = 1,
        PageSize = 10,
        TotalCount = 0,
        TotalPages = 0,
        Items = new List<LabIndicatorMaster>(),
    };

    private static LabIndicatorMaster MakeIndicator(
        Guid? id = null,
        string symbol = "HGB",
        bool isDeleted = false,
        double? min = 12,
        double? max = 16) => new()
        {
            Id = id ?? IndicatorId,
            Symbol = symbol,
            FullName = "Hemoglobin",
            Unit = "g/dL",
            MinReference = min,
            MaxReference = max,
            Category = "Hematology",
            IsActive = true,
            IsDeleted = isDeleted,
        };

    private static LabIndicatorAlias MakeAlias(
        Guid? id = null,
        Guid? indicatorId = null,
        string aliasText = "Huyet sac to",
        bool isDeleted = false) => new()
        {
            Id = id ?? ChildId,
            IndicatorId = indicatorId ?? IndicatorId,
            AliasText = aliasText,
            Language = "vi",
            IsPrimary = false,
            IsDeleted = isDeleted,
        };

    private static LabIndicatorReferenceRange MakeRange(
        Guid? id = null,
        Guid? indicatorId = null,
        Gender? gender = null,
        AgeGroup? ageGroup = null,
        bool isDeleted = false) => new()
        {
            Id = id ?? ChildId,
            IndicatorId = indicatorId ?? IndicatorId,
            Gender = gender,
            AgeGroup = ageGroup,
            ComparisonType = ReferenceComparisonType.Between,
            MinValue = 12,
            MaxValue = 16,
            IsDeleted = isDeleted,
        };

    private static LabIndicatorAdviceCache MakeAdvice(
        Guid? id = null,
        Guid? indicatorId = null,
        LabResultStatus status = LabResultStatus.High,
        bool isDeleted = false) => new()
        {
            Id = id ?? ChildId,
            IndicatorId = indicatorId ?? IndicatorId,
            Status = status,
            DisplayTitle = "High hemoglobin",
            IsDeleted = isDeleted,
        };

    private static CreateLabIndicatorRequest MakeCreateRequest(
        string symbol = "hgb",
        double? min = 12,
        double? max = 16) => new()
        {
            Symbol = symbol,
            FullName = " Hemoglobin ",
            Unit = " g/dL ",
            MinReference = min,
            MaxReference = max,
            Description = "   ",
            Category = "Hematology",
        };

    private static CreateLabIndicatorReferenceRangeRequest MakeCreateRangeRequest(
        Gender? gender = null,
        AgeGroup? ageGroup = null,
        ReferenceComparisonType comparisonType = ReferenceComparisonType.Between,
        double? min = 12,
        double? max = 16) => new()
        {
            Gender = gender,
            AgeGroup = ageGroup,
            ComparisonType = comparisonType,
            MinValue = min,
            MaxValue = max,
            Unit = " g/dL ",
        };

    private static UpdateLabIndicatorReferenceRangeRequest MakeUpdateRangeRequest(
        Gender? gender = null,
        AgeGroup? ageGroup = null,
        ReferenceComparisonType comparisonType = ReferenceComparisonType.Between,
        double? min = 10,
        double? max = 20) => new()
        {
            Gender = gender,
            AgeGroup = ageGroup,
            ComparisonType = comparisonType,
            MinValue = min,
            MaxValue = max,
            Unit = "g/dL",
        };

    private static CreateLabIndicatorAdviceCacheRequest MakeCreateAdviceRequest(
        LabResultStatus status = LabResultStatus.High) => new()
        {
            Status = status,
            DisplayTitle = " High hemoglobin ",
            Summary = "   ",
            SeverityLevel = LabAdviceSeverityLevel.Warning,
        };

    private void IndicatorNotFound() =>
        _indicatorRepoMock.Setup(r => r.GetByIdAsync(IndicatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabIndicatorMaster?)null);

    // â”€â”€ ListLabIndicatorsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task ListLabIndicatorsAsync_ItemsExist_ReturnsMappedPage()
    {
        SetupPagedIndicators(new PagedResult<LabIndicatorMaster>
        {
            PageNumber = 2,
            PageSize = 5,
            TotalCount = 6,
            TotalPages = 2,
            Items = new List<LabIndicatorMaster> { MakeIndicator() },
        });

        var result = await _service.ListLabIndicatorsAsync(2, 5);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Symbol, Is.EqualTo("HGB"));
    }

    [Test]
    [Category("N")]
    public async Task ListLabIndicatorsAsync_ItemsExist_PreservesPagingMetadata()
    {
        SetupPagedIndicators(new PagedResult<LabIndicatorMaster>
        {
            PageNumber = 3,
            PageSize = 20,
            TotalCount = 41,
            TotalPages = 3,
            Items = new List<LabIndicatorMaster>(),
        });

        var result = await _service.ListLabIndicatorsAsync(3, 20);

        Assert.That(result.TotalCount, Is.EqualTo(41));
        Assert.That(result.TotalPages, Is.EqualTo(3));
    }

    [Test]
    [Category("B")]
    public async Task ListLabIndicatorsAsync_NoResults_ReturnsEmptyItems()
    {
        SetupPagedIndicators(EmptyPage());

        var result = await _service.ListLabIndicatorsAsync(1, 10);

        Assert.That(result.Items, Is.Empty);
    }

    [Test]
    [Category("N")]
    public async Task ListLabIndicatorsAsync_SearchTerm_PredicateMatchesOnCategoryIgnoringCase()
    {
        Expression<Func<LabIndicatorMaster, bool>>? predicate = null;
        _indicatorRepoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<LabIndicatorMaster, bool>>>(),
                It.IsAny<Func<IQueryable<LabIndicatorMaster>, IOrderedQueryable<LabIndicatorMaster>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback(new Action<int, int, Expression<Func<LabIndicatorMaster, bool>>?,
                Func<IQueryable<LabIndicatorMaster>, IOrderedQueryable<LabIndicatorMaster>>?, bool, CancellationToken>(
                (_, _, p, _, _, _) => predicate = p))
            .ReturnsAsync(EmptyPage());

        await _service.ListLabIndicatorsAsync(1, 10, "  HEMATOLOGY  ");

        var match = predicate!.Compile();
        Assert.That(match(MakeIndicator()), Is.True);
    }

    [Test]
    [Category("B")]
    public async Task ListLabIndicatorsAsync_AnySearch_PredicateExcludesDeletedIndicators()
    {
        Expression<Func<LabIndicatorMaster, bool>>? predicate = null;
        _indicatorRepoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<LabIndicatorMaster, bool>>>(),
                It.IsAny<Func<IQueryable<LabIndicatorMaster>, IOrderedQueryable<LabIndicatorMaster>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback(new Action<int, int, Expression<Func<LabIndicatorMaster, bool>>?,
                Func<IQueryable<LabIndicatorMaster>, IOrderedQueryable<LabIndicatorMaster>>?, bool, CancellationToken>(
                (_, _, p, _, _, _) => predicate = p))
            .ReturnsAsync(EmptyPage());

        await _service.ListLabIndicatorsAsync(1, 10, "   ");

        var match = predicate!.Compile();
        Assert.That(match(MakeIndicator(isDeleted: true)), Is.False);
    }

    // â”€â”€ GetLabIndicatorByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task GetLabIndicatorByIdAsync_EmptyId_ReturnsNull()
    {
        var result = await _service.GetLabIndicatorByIdAsync(Guid.Empty);

        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task GetLabIndicatorByIdAsync_NotFound_ReturnsNull()
    {
        _indicatorRepoMock.Setup(r => r.GetByIdWithDetailsAsync(IndicatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabIndicatorMaster?)null);

        var result = await _service.GetLabIndicatorByIdAsync(IndicatorId);

        Assert.That(result, Is.Null);
    }

    [Test]
    [Category("N")]
    public async Task GetLabIndicatorByIdAsync_Found_ReturnsMappedDetail()
    {
        _indicatorRepoMock.Setup(r => r.GetByIdWithDetailsAsync(IndicatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeIndicator());

        var result = await _service.GetLabIndicatorByIdAsync(IndicatorId);

        Assert.That(result!.IndicatorId, Is.EqualTo(IndicatorId));
    }

    // â”€â”€ CreateLabIndicatorAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task CreateLabIndicatorAsync_ValidRequest_PersistsUppercasedSymbol()
    {
        LabIndicatorMaster? captured = null;
        _indicatorRepoMock.Setup(r => r.Add(It.IsAny<LabIndicatorMaster>()))
            .Callback<LabIndicatorMaster>(e => captured = e);

        var result = await _service.CreateLabIndicatorAsync(MakeCreateRequest(symbol: "  hgb  "));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(captured!.Symbol, Is.EqualTo("HGB"));
    }

    [Test]
    [Category("B")]
    public async Task CreateLabIndicatorAsync_ValidRequest_NormalizesWhitespaceOnlyTextToNull()
    {
        LabIndicatorMaster? captured = null;
        _indicatorRepoMock.Setup(r => r.Add(It.IsAny<LabIndicatorMaster>()))
            .Callback<LabIndicatorMaster>(e => captured = e);

        await _service.CreateLabIndicatorAsync(MakeCreateRequest());

        Assert.That(captured!.Description, Is.Null);
        Assert.That(captured.FullName, Is.EqualTo("Hemoglobin"));
    }

    [Test]
    [Category("A")]
    public async Task CreateLabIndicatorAsync_NullRequest_ReturnsRequiredBodyError()
    {
        var result = await _service.CreateLabIndicatorAsync(null!);

        Assert.That(result.Errors, Does.Contain("Request body là bắt buộc"));
    }

    [Test]
    [Category("B")]
    public async Task CreateLabIndicatorAsync_BlankSymbol_ReturnsSymbolRequiredError()
    {
        var result = await _service.CreateLabIndicatorAsync(MakeCreateRequest(symbol: "   "));

        Assert.That(result.Errors, Does.Contain("Symbol là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task CreateLabIndicatorAsync_MinGreaterThanMax_ReturnsRangeError()
    {
        var result = await _service.CreateLabIndicatorAsync(MakeCreateRequest(min: 20, max: 10));

        Assert.That(result.Errors, Does.Contain("MinReference không được lớn hơn MaxReference"));
    }

    [Test]
    [Category("B")]
    public async Task CreateLabIndicatorAsync_MinEqualsMax_Succeeds()
    {
        var result = await _service.CreateLabIndicatorAsync(MakeCreateRequest(min: 15, max: 15));

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task CreateLabIndicatorAsync_SymbolAlreadyExists_ReturnsDuplicateError()
    {
        _indicatorRepoMock.Setup(r => r.SymbolExistsAsync(
                "HGB", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.CreateLabIndicatorAsync(MakeCreateRequest());

        Assert.That(result.Errors, Does.Contain("Ký hiệu chỉ số đã tồn tại"));
    }

    // â”€â”€ BulkCreateLabIndicatorsAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task BulkCreateLabIndicatorsAsync_TwoValidItems_AddsBothEntities()
    {
        var captured = new List<LabIndicatorMaster>();
        _indicatorRepoMock.Setup(r => r.Add(It.IsAny<LabIndicatorMaster>()))
            .Callback<LabIndicatorMaster>(captured.Add);

        var result = await _service.BulkCreateLabIndicatorsAsync(new BulkCreateLabIndicatorsRequest
        {
            Indicators = new List<CreateLabIndicatorRequest>
            {
                MakeCreateRequest(symbol: "hgb"),
                MakeCreateRequest(symbol: "wbc"),
            },
        });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(captured, Has.Count.EqualTo(2));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateLabIndicatorsAsync_NullRequest_ReturnsAtLeastOneRequiredError()
    {
        var result = await _service.BulkCreateLabIndicatorsAsync(null!);

        Assert.That(result.Errors, Does.Contain("At least one indicator is required."));
    }

    [Test]
    [Category("B")]
    public async Task BulkCreateLabIndicatorsAsync_EmptyList_ReturnsAtLeastOneRequiredError()
    {
        var result = await _service.BulkCreateLabIndicatorsAsync(new BulkCreateLabIndicatorsRequest
        {
            Indicators = new List<CreateLabIndicatorRequest>(),
        });

        Assert.That(result.Errors, Does.Contain("At least one indicator is required."));
    }

    [Test]
    [Category("B")]
    public async Task BulkCreateLabIndicatorsAsync_InvalidItem_ReturnsIndexPrefixedError()
    {
        var result = await _service.BulkCreateLabIndicatorsAsync(new BulkCreateLabIndicatorsRequest
        {
            Indicators = new List<CreateLabIndicatorRequest>
            {
                MakeCreateRequest(symbol: "hgb"),
                MakeCreateRequest(symbol: ""),
            },
        });

        Assert.That(result.Errors, Does.Contain("Indicators[1]: Symbol là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateLabIndicatorsAsync_DuplicateSymbolInRequest_ReturnsDuplicateError()
    {
        var result = await _service.BulkCreateLabIndicatorsAsync(new BulkCreateLabIndicatorsRequest
        {
            Indicators = new List<CreateLabIndicatorRequest>
            {
                MakeCreateRequest(symbol: "hgb"),
                MakeCreateRequest(symbol: "HGB"),
            },
        });

        Assert.That(result.Errors, Does.Contain("Duplicate symbol in request: HGB"));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateLabIndicatorsAsync_SymbolAlreadyPersisted_ReturnsExistsError()
    {
        _indicatorRepoMock.Setup(r => r.SymbolExistsAsync(
                "HGB", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.BulkCreateLabIndicatorsAsync(new BulkCreateLabIndicatorsRequest
        {
            Indicators = new List<CreateLabIndicatorRequest> { MakeCreateRequest(symbol: "hgb") },
        });

        Assert.That(result.Errors, Does.Contain("Ký hiệu chỉ số đã tồn tại: HGB"));
    }

    // â”€â”€ UpdateLabIndicatorAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task UpdateLabIndicatorAsync_EmptyId_ReturnsInvalidIdWithoutNotFound()
    {
        var result = await _service.UpdateLabIndicatorAsync(Guid.Empty, new UpdateLabIndicatorRequest());

        Assert.That(result.Errors, Does.Contain("Id chỉ số xét nghiệm không hợp lệ"));
        Assert.That(result.NotFound, Is.False);
    }

    [Test]
    [Category("A")]
    public async Task UpdateLabIndicatorAsync_NullRequest_ReturnsRequiredBodyError()
    {
        var result = await _service.UpdateLabIndicatorAsync(IndicatorId, null!);

        Assert.That(result.Errors, Does.Contain("Request body là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateLabIndicatorAsync_IndicatorMissing_ReturnsNotFound()
    {
        IndicatorNotFound();

        var result = await _service.UpdateLabIndicatorAsync(IndicatorId, new UpdateLabIndicatorRequest());

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task UpdateLabIndicatorAsync_IndicatorSoftDeleted_ReturnsNotFound()
    {
        _existingIndicator = MakeIndicator(isDeleted: true);

        var result = await _service.UpdateLabIndicatorAsync(IndicatorId, new UpdateLabIndicatorRequest());

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("B")]
    public async Task UpdateLabIndicatorAsync_BlankSymbolProvided_ReturnsCannotBeEmptyError()
    {
        var result = await _service.UpdateLabIndicatorAsync(
            IndicatorId, new UpdateLabIndicatorRequest { Symbol = "  " });

        Assert.That(result.Errors, Does.Contain("Symbol không được để trống"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateLabIndicatorAsync_SymbolTakenByAnotherIndicator_ReturnsDuplicateError()
    {
        _indicatorRepoMock.Setup(r => r.SymbolExistsAsync("WBC", IndicatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.UpdateLabIndicatorAsync(
            IndicatorId, new UpdateLabIndicatorRequest { Symbol = "wbc" });

        Assert.That(result.Errors, Does.Contain("Ký hiệu chỉ số đã tồn tại"));
    }

    [Test]
    [Category("B")]
    public async Task UpdateLabIndicatorAsync_ValidPartialUpdate_AppliesTrimmedValues()
    {
        var result = await _service.UpdateLabIndicatorAsync(IndicatorId, new UpdateLabIndicatorRequest
        {
            Symbol = " wbc ",
            FullName = "  White Blood Cell  ",
            Unit = "   ",
            IsActive = false,
        });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(_existingIndicator.Symbol, Is.EqualTo("WBC"));
        Assert.That(_existingIndicator.FullName, Is.EqualTo("White Blood Cell"));
        Assert.That(_existingIndicator.Unit, Is.Null);
        Assert.That(_existingIndicator.IsActive, Is.False);
    }

    [Test]
    [Category("A")]
    public async Task UpdateLabIndicatorAsync_NewMinExceedsExistingMax_ReturnsRangeError()
    {
        var result = await _service.UpdateLabIndicatorAsync(
            IndicatorId, new UpdateLabIndicatorRequest { MinReference = 99 });

        Assert.That(result.Errors, Does.Contain("MinReference không được lớn hơn MaxReference"));
    }

    // â”€â”€ SoftDeleteLabIndicatorAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task SoftDeleteLabIndicatorAsync_EmptyId_ReturnsInvalidIdError()
    {
        var result = await _service.SoftDeleteLabIndicatorAsync(Guid.Empty);

        Assert.That(result.Errors, Does.Contain("Id chỉ số xét nghiệm không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteLabIndicatorAsync_IndicatorMissing_ReturnsNotFound()
    {
        IndicatorNotFound();

        var result = await _service.SoftDeleteLabIndicatorAsync(IndicatorId);

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteLabIndicatorAsync_AlreadyDeleted_ReturnsNotFound()
    {
        _existingIndicator = MakeIndicator(isDeleted: true);

        var result = await _service.SoftDeleteLabIndicatorAsync(IndicatorId);

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("N")]
    public async Task SoftDeleteLabIndicatorAsync_Found_MarksDeletedAndInactive()
    {
        var result = await _service.SoftDeleteLabIndicatorAsync(IndicatorId);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(_existingIndicator.IsDeleted, Is.True);
        Assert.That(_existingIndicator.IsActive, Is.False);
        Assert.That(_existingIndicator.DeletedAt, Is.Not.Null);
    }

    // â”€â”€ GetAliasesByIndicatorIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task GetAliasesByIndicatorIdAsync_AliasesExist_ReturnsMappedAliases()
    {
        SetupAliasQuery(new[] { MakeAlias() });

        var result = await _service.GetAliasesByIndicatorIdAsync(IndicatorId);

        Assert.That(result.Data, Has.Count.EqualTo(1));
        Assert.That(result.Data![0].AliasText, Is.EqualTo("Huyet sac to"));
    }

    [Test]
    [Category("B")]
    public async Task GetAliasesByIndicatorIdAsync_EmptyIndicatorId_ReturnsInvalidIdError()
    {
        var result = await _service.GetAliasesByIndicatorIdAsync(Guid.Empty);

        Assert.That(result.Errors, Does.Contain("Id chỉ số xét nghiệm không hợp lệ"));
        Assert.That(result.NotFound, Is.False);
    }

    [Test]
    [Category("A")]
    public async Task GetAliasesByIndicatorIdAsync_IndicatorMissing_ReturnsNotFound()
    {
        IndicatorNotFound();

        var result = await _service.GetAliasesByIndicatorIdAsync(IndicatorId);

        Assert.That(result.NotFound, Is.True);
    }

    // â”€â”€ GetReferenceRangesByIndicatorIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task GetReferenceRangesByIndicatorIdAsync_RangesExist_ReturnsMappedRanges()
    {
        SetupRangeQuery(new[] { MakeRange(gender: Gender.Male) });

        var result = await _service.GetReferenceRangesByIndicatorIdAsync(IndicatorId);

        Assert.That(result.Data, Has.Count.EqualTo(1));
        Assert.That(result.Data![0].Gender, Is.EqualTo(Gender.Male));
    }

    [Test]
    [Category("A")]
    public async Task GetReferenceRangesByIndicatorIdAsync_IndicatorMissing_ReturnsNotFound()
    {
        IndicatorNotFound();

        var result = await _service.GetReferenceRangesByIndicatorIdAsync(IndicatorId);

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("B")]
    public async Task GetReferenceRangesByIndicatorIdAsync_NoRanges_ReturnsEmptyData()
    {
        var result = await _service.GetReferenceRangesByIndicatorIdAsync(IndicatorId);

        Assert.That(result.Data, Is.Empty);
    }

    // â”€â”€ GetAdviceCachesByIndicatorIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task GetAdviceCachesByIndicatorIdAsync_CachesExist_ReturnsMappedCaches()
    {
        SetupAdviceQuery(new[] { MakeAdvice() });

        var result = await _service.GetAdviceCachesByIndicatorIdAsync(IndicatorId);

        Assert.That(result.Data, Has.Count.EqualTo(1));
        Assert.That(result.Data![0].Status, Is.EqualTo(LabResultStatus.High));
    }

    [Test]
    [Category("A")]
    public async Task GetAdviceCachesByIndicatorIdAsync_IndicatorMissing_ReturnsNotFound()
    {
        IndicatorNotFound();

        var result = await _service.GetAdviceCachesByIndicatorIdAsync(IndicatorId);

        Assert.That(result.NotFound, Is.True);
    }

    // â”€â”€ BulkCreateAliasesAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task BulkCreateAliasesAsync_TwoValidAliases_AddsBothTrimmed()
    {
        var captured = new List<LabIndicatorAlias>();
        _aliasRepoMock.Setup(r => r.Add(It.IsAny<LabIndicatorAlias>()))
            .Callback<LabIndicatorAlias>(captured.Add);

        var result = await _service.BulkCreateAliasesAsync(IndicatorId, new BulkCreateLabIndicatorAliasesRequest
        {
            Aliases = new List<CreateLabIndicatorAliasRequest>
            {
                new() { AliasText = "  Hemoglobin  ", Language = "  en  " },
                new() { AliasText = "Huyet sac to", Language = "   " },
            },
        });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(captured[0].AliasText, Is.EqualTo("Hemoglobin"));
        Assert.That(captured[0].Language, Is.EqualTo("en"));
        Assert.That(captured[1].Language, Is.Null);
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateAliasesAsync_IndicatorMissing_ReturnsNotFound()
    {
        IndicatorNotFound();

        var result = await _service.BulkCreateAliasesAsync(
            IndicatorId, new BulkCreateLabIndicatorAliasesRequest());

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("B")]
    public async Task BulkCreateAliasesAsync_EmptyList_ReturnsAtLeastOneRequiredError()
    {
        var result = await _service.BulkCreateAliasesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorAliasesRequest { Aliases = new List<CreateLabIndicatorAliasRequest>() });

        Assert.That(result.Errors, Does.Contain("At least one alias is required."));
    }

    [Test]
    [Category("B")]
    public async Task BulkCreateAliasesAsync_BlankAliasText_ReturnsIndexPrefixedError()
    {
        var result = await _service.BulkCreateAliasesAsync(IndicatorId, new BulkCreateLabIndicatorAliasesRequest
        {
            Aliases = new List<CreateLabIndicatorAliasRequest>
            {
                new() { AliasText = "Hemoglobin" },
                new() { AliasText = "   " },
            },
        });

        Assert.That(result.Errors, Does.Contain("Aliases[1]: AliasText là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateAliasesAsync_DuplicateAliasInRequest_ReturnsDuplicateError()
    {
        var result = await _service.BulkCreateAliasesAsync(IndicatorId, new BulkCreateLabIndicatorAliasesRequest
        {
            Aliases = new List<CreateLabIndicatorAliasRequest>
            {
                new() { AliasText = "Hemoglobin" },
                new() { AliasText = "hemoglobin" },
            },
        });

        Assert.That(result.Errors, Does.Contain("Duplicate alias in request: Hemoglobin"));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateAliasesAsync_AliasAlreadyPersisted_ReturnsExistsError()
    {
        _aliasRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<LabIndicatorAlias, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAlias());

        var result = await _service.BulkCreateAliasesAsync(IndicatorId, new BulkCreateLabIndicatorAliasesRequest
        {
            Aliases = new List<CreateLabIndicatorAliasRequest> { new() { AliasText = "Hemoglobin" } },
        });

        Assert.That(result.Errors, Does.Contain("Alias đã tồn tại cho chỉ số này: Hemoglobin"));
    }

    // â”€â”€ CreateAliasAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task CreateAliasAsync_ValidRequest_PersistsTrimmedAlias()
    {
        LabIndicatorAlias? captured = null;
        _aliasRepoMock.Setup(r => r.Add(It.IsAny<LabIndicatorAlias>()))
            .Callback<LabIndicatorAlias>(e => captured = e);

        var result = await _service.CreateAliasAsync(IndicatorId, new CreateLabIndicatorAliasRequest
        {
            AliasText = "  Hemoglobin  ",
            Language = "  en  ",
            IsPrimary = true,
        });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(captured!.AliasText, Is.EqualTo("Hemoglobin"));
        Assert.That(captured.IndicatorId, Is.EqualTo(IndicatorId));
    }

    [Test]
    [Category("A")]
    public async Task CreateAliasAsync_IndicatorMissing_ReturnsNotFound()
    {
        IndicatorNotFound();

        var result = await _service.CreateAliasAsync(
            IndicatorId, new CreateLabIndicatorAliasRequest { AliasText = "Hemoglobin" });

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("B")]
    public async Task CreateAliasAsync_BlankAliasText_ReturnsRequiredError()
    {
        var result = await _service.CreateAliasAsync(
            IndicatorId, new CreateLabIndicatorAliasRequest { AliasText = "   " });

        Assert.That(result.Errors, Does.Contain("AliasText là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task CreateAliasAsync_AliasAlreadyExists_ReturnsExistsError()
    {
        _aliasRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<LabIndicatorAlias, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAlias());

        var result = await _service.CreateAliasAsync(
            IndicatorId, new CreateLabIndicatorAliasRequest { AliasText = "Hemoglobin" });

        Assert.That(result.Errors, Does.Contain("Alias đã tồn tại cho chỉ số này: Hemoglobin"));
    }

    // â”€â”€ UpdateAliasAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task UpdateAliasAsync_ValidRequest_AppliesNewValues()
    {
        var alias = MakeAlias();
        _aliasRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>())).ReturnsAsync(alias);

        var result = await _service.UpdateAliasAsync(IndicatorId, ChildId, new UpdateLabIndicatorAliasRequest
        {
            AliasText = "  Hemoglobin  ",
            Language = "   ",
            IsPrimary = true,
        });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(alias.AliasText, Is.EqualTo("Hemoglobin"));
        Assert.That(alias.Language, Is.Null);
        Assert.That(alias.IsPrimary, Is.True);
    }

    [Test]
    [Category("B")]
    public async Task UpdateAliasAsync_EmptyAliasId_ReturnsInvalidIdError()
    {
        var result = await _service.UpdateAliasAsync(
            IndicatorId, Guid.Empty, new UpdateLabIndicatorAliasRequest { AliasText = "Hemoglobin" });

        Assert.That(result.Errors, Does.Contain("Id alias không hợp lệ"));
    }

    [Test]
    [Category("B")]
    public async Task UpdateAliasAsync_BlankAliasText_ReturnsRequiredError()
    {
        var result = await _service.UpdateAliasAsync(
            IndicatorId, ChildId, new UpdateLabIndicatorAliasRequest { AliasText = "   " });

        Assert.That(result.Errors, Does.Contain("AliasText là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateAliasAsync_AliasMissing_ReturnsNotFound()
    {
        _aliasRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabIndicatorAlias?)null);

        var result = await _service.UpdateAliasAsync(
            IndicatorId, ChildId, new UpdateLabIndicatorAliasRequest { AliasText = "Hemoglobin" });

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task UpdateAliasAsync_AliasBelongsToAnotherIndicator_ReturnsNotFound()
    {
        _aliasRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAlias(indicatorId: OtherIndicatorId));

        var result = await _service.UpdateAliasAsync(
            IndicatorId, ChildId, new UpdateLabIndicatorAliasRequest { AliasText = "Hemoglobin" });

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task UpdateAliasAsync_DuplicateAliasText_ReturnsExistsError()
    {
        _aliasRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAlias());
        _aliasRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<LabIndicatorAlias, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAlias(id: Guid.NewGuid()));

        var result = await _service.UpdateAliasAsync(
            IndicatorId, ChildId, new UpdateLabIndicatorAliasRequest { AliasText = "Hemoglobin" });

        Assert.That(result.Errors, Does.Contain("Alias đã tồn tại cho chỉ số này: Hemoglobin"));
    }

    // â”€â”€ SoftDeleteAliasAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task SoftDeleteAliasAsync_Found_MarksAliasDeleted()
    {
        var alias = MakeAlias();
        _aliasRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>())).ReturnsAsync(alias);

        var result = await _service.SoftDeleteAliasAsync(IndicatorId, ChildId);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(alias.IsDeleted, Is.True);
        Assert.That(alias.DeletedAt, Is.Not.Null);
    }

    [Test]
    [Category("B")]
    public async Task SoftDeleteAliasAsync_EmptyAliasId_ReturnsInvalidIdError()
    {
        var result = await _service.SoftDeleteAliasAsync(IndicatorId, Guid.Empty);

        Assert.That(result.Errors, Does.Contain("Id alias không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteAliasAsync_AliasAlreadyDeleted_ReturnsNotFound()
    {
        _aliasRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAlias(isDeleted: true));

        var result = await _service.SoftDeleteAliasAsync(IndicatorId, ChildId);

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteAliasAsync_AliasBelongsToAnotherIndicator_ReturnsNotFound()
    {
        _aliasRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAlias(indicatorId: OtherIndicatorId));

        var result = await _service.SoftDeleteAliasAsync(IndicatorId, ChildId);

        Assert.That(result.NotFound, Is.True);
    }

    // â”€â”€ BulkCreateReferenceRangesAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task BulkCreateReferenceRangesAsync_ValidRanges_AddsEntities()
    {
        var captured = new List<LabIndicatorReferenceRange>();
        _rangeRepoMock.Setup(r => r.Add(It.IsAny<LabIndicatorReferenceRange>()))
            .Callback<LabIndicatorReferenceRange>(captured.Add);

        var result = await _service.BulkCreateReferenceRangesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorReferenceRangesRequest
            {
                ReferenceRanges = new List<CreateLabIndicatorReferenceRangeRequest>
                {
                    MakeCreateRangeRequest(gender: Gender.Male),
                    MakeCreateRangeRequest(gender: Gender.Female),
                },
            });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(captured, Has.Count.EqualTo(2));
        Assert.That(captured[0].Unit, Is.EqualTo("g/dL"));
    }

    [Test]
    [Category("B")]
    public async Task BulkCreateReferenceRangesAsync_EmptyList_ReturnsAtLeastOneRequiredError()
    {
        var result = await _service.BulkCreateReferenceRangesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorReferenceRangesRequest
            {
                ReferenceRanges = new List<CreateLabIndicatorReferenceRangeRequest>(),
            });

        Assert.That(result.Errors, Does.Contain("At least one reference range is required."));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateReferenceRangesAsync_IndicatorMissing_ReturnsNotFound()
    {
        IndicatorNotFound();

        var result = await _service.BulkCreateReferenceRangesAsync(
            IndicatorId, new BulkCreateLabIndicatorReferenceRangesRequest());

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateReferenceRangesAsync_BetweenMissingMaxValue_ReturnsIndexPrefixedError()
    {
        var result = await _service.BulkCreateReferenceRangesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorReferenceRangesRequest
            {
                ReferenceRanges = new List<CreateLabIndicatorReferenceRangeRequest>
                {
                    MakeCreateRangeRequest(max: null),
                },
            });

        Assert.That(
            result.Errors,
            Does.Contain("ReferenceRanges[0]: So sánh Between yêu cầu MinValue và MaxValue"));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateReferenceRangesAsync_GenderAndAgeGroupBothSet_ReturnsDimensionError()
    {
        var result = await _service.BulkCreateReferenceRangesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorReferenceRangesRequest
            {
                ReferenceRanges = new List<CreateLabIndicatorReferenceRangeRequest>
                {
                    MakeCreateRangeRequest(gender: Gender.Male, ageGroup: AgeGroup.Adult),
                },
            });

        Assert.That(
            result.Errors,
            Does.Contain("ReferenceRanges[0]: Khoảng tham chiếu không thể đặt cả Gender và AgeGroup"));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateReferenceRangesAsync_DuplicateGenderWithinBatch_ReturnsUniquenessError()
    {
        var result = await _service.BulkCreateReferenceRangesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorReferenceRangesRequest
            {
                ReferenceRanges = new List<CreateLabIndicatorReferenceRangeRequest>
                {
                    MakeCreateRangeRequest(gender: Gender.Male),
                    MakeCreateRangeRequest(gender: Gender.Male),
                },
            });

        Assert.That(
            result.Errors,
            Does.Contain("ReferenceRanges[1]: Khoảng tham chiếu cho giới tính Male đã tồn tại."));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateReferenceRangesAsync_DefaultRangeAlreadyPersisted_ReturnsUniquenessError()
    {
        SetupRangeQuery(new[] { MakeRange() });

        var result = await _service.BulkCreateReferenceRangesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorReferenceRangesRequest
            {
                ReferenceRanges = new List<CreateLabIndicatorReferenceRangeRequest> { MakeCreateRangeRequest() },
            });

        Assert.That(
            result.Errors,
            Does.Contain("ReferenceRanges[0]: Khoảng tham chiếu mặc định đã tồn tại cho chỉ số này"));
    }

    // â”€â”€ CreateReferenceRangeAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task CreateReferenceRangeAsync_ValidDefaultRange_PersistsEntity()
    {
        LabIndicatorReferenceRange? captured = null;
        _rangeRepoMock.Setup(r => r.Add(It.IsAny<LabIndicatorReferenceRange>()))
            .Callback<LabIndicatorReferenceRange>(e => captured = e);

        var result = await _service.CreateReferenceRangeAsync(IndicatorId, MakeCreateRangeRequest());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(captured!.IndicatorId, Is.EqualTo(IndicatorId));
        Assert.That(captured.Unit, Is.EqualTo("g/dL"));
    }

    [Test]
    [Category("A")]
    public async Task CreateReferenceRangeAsync_IndicatorMissing_ReturnsNotFound()
    {
        IndicatorNotFound();

        var result = await _service.CreateReferenceRangeAsync(IndicatorId, MakeCreateRangeRequest());

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task CreateReferenceRangeAsync_NullRequest_ReturnsRequiredError()
    {
        var result = await _service.CreateReferenceRangeAsync(IndicatorId, null!);

        Assert.That(result.Errors, Does.Contain("Request khoảng tham chiếu là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task CreateReferenceRangeAsync_LessThanOrEqualWithoutMaxValue_ReturnsValidationError()
    {
        var result = await _service.CreateReferenceRangeAsync(
            IndicatorId,
            MakeCreateRangeRequest(comparisonType: ReferenceComparisonType.LessThanOrEqual, max: null));

        Assert.That(result.Errors, Does.Contain("So sánh LessThanOrEqual yêu cầu MaxValue"));
    }

    [Test]
    [Category("A")]
    public async Task CreateReferenceRangeAsync_GreaterThanOrEqualWithoutMinValue_ReturnsValidationError()
    {
        var result = await _service.CreateReferenceRangeAsync(
            IndicatorId,
            MakeCreateRangeRequest(comparisonType: ReferenceComparisonType.GreaterThanOrEqual, min: null));

        Assert.That(result.Errors, Does.Contain("So sánh GreaterThanOrEqual yêu cầu MinValue"));
    }

    [Test]
    [Category("A")]
    public async Task CreateReferenceRangeAsync_MinGreaterThanMax_ReturnsValidationError()
    {
        var result = await _service.CreateReferenceRangeAsync(
            IndicatorId, MakeCreateRangeRequest(min: 20, max: 10));

        Assert.That(result.Errors, Does.Contain("MinValue không được lớn hơn MaxValue"));
    }

    [Test]
    [Category("A")]
    public async Task CreateReferenceRangeAsync_GenderRangeAlreadyExists_ReturnsUniquenessError()
    {
        SetupRangeQuery(new[] { MakeRange(gender: Gender.Male) });

        var result = await _service.CreateReferenceRangeAsync(
            IndicatorId, MakeCreateRangeRequest(gender: Gender.Male));

        Assert.That(result.Errors, Does.Contain("Khoảng tham chiếu cho giới tính Male đã tồn tại."));
    }

    // â”€â”€ UpdateReferenceRangeAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task UpdateReferenceRangeAsync_ValidRequest_AppliesNewValues()
    {
        var range = MakeRange();
        _rangeRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>())).ReturnsAsync(range);

        var result = await _service.UpdateReferenceRangeAsync(
            IndicatorId, ChildId, MakeUpdateRangeRequest(ageGroup: AgeGroup.Child));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(range.AgeGroup, Is.EqualTo(AgeGroup.Child));
        Assert.That(range.MinValue, Is.EqualTo(10));
        Assert.That(range.MaxValue, Is.EqualTo(20));
    }

    [Test]
    [Category("B")]
    public async Task UpdateReferenceRangeAsync_EmptyRangeId_ReturnsInvalidIdError()
    {
        var result = await _service.UpdateReferenceRangeAsync(
            IndicatorId, Guid.Empty, MakeUpdateRangeRequest());

        Assert.That(result.Errors, Does.Contain("Id khoảng tham chiếu không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateReferenceRangeAsync_NullRequest_ReturnsRequiredError()
    {
        var result = await _service.UpdateReferenceRangeAsync(IndicatorId, ChildId, null!);

        Assert.That(result.Errors, Does.Contain("Request khoảng tham chiếu là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateReferenceRangeAsync_RangeBelongsToAnotherIndicator_ReturnsNotFound()
    {
        _rangeRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRange(indicatorId: OtherIndicatorId));

        var result = await _service.UpdateReferenceRangeAsync(IndicatorId, ChildId, MakeUpdateRangeRequest());

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task UpdateReferenceRangeAsync_GenderAndAgeGroupBothSet_ReturnsDimensionError()
    {
        _rangeRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRange());

        var result = await _service.UpdateReferenceRangeAsync(
            IndicatorId, ChildId, MakeUpdateRangeRequest(gender: Gender.Male, ageGroup: AgeGroup.Adult));

        Assert.That(result.Errors, Does.Contain("Khoảng tham chiếu không thể đặt cả Gender và AgeGroup"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateReferenceRangeAsync_ConflictsWithSiblingRange_ReturnsUniquenessError()
    {
        _rangeRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRange());
        SetupRangeQuery(new[] { MakeRange(id: Guid.NewGuid(), ageGroup: AgeGroup.Adult) });

        var result = await _service.UpdateReferenceRangeAsync(
            IndicatorId, ChildId, MakeUpdateRangeRequest(ageGroup: AgeGroup.Adult));

        Assert.That(result.Errors, Does.Contain("Khoảng tham chiếu cho nhóm tuổi Adult đã tồn tại."));
    }

    // â”€â”€ SoftDeleteReferenceRangeAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task SoftDeleteReferenceRangeAsync_Found_MarksRangeDeleted()
    {
        var range = MakeRange();
        _rangeRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>())).ReturnsAsync(range);

        var result = await _service.SoftDeleteReferenceRangeAsync(IndicatorId, ChildId);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(range.IsDeleted, Is.True);
        Assert.That(range.DeletedAt, Is.Not.Null);
    }

    [Test]
    [Category("B")]
    public async Task SoftDeleteReferenceRangeAsync_EmptyRangeId_ReturnsInvalidIdError()
    {
        var result = await _service.SoftDeleteReferenceRangeAsync(IndicatorId, Guid.Empty);

        Assert.That(result.Errors, Does.Contain("Id khoảng tham chiếu không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteReferenceRangeAsync_RangeMissing_ReturnsNotFound()
    {
        _rangeRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabIndicatorReferenceRange?)null);

        var result = await _service.SoftDeleteReferenceRangeAsync(IndicatorId, ChildId);

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteReferenceRangeAsync_RangeAlreadyDeleted_ReturnsNotFound()
    {
        _rangeRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRange(isDeleted: true));

        var result = await _service.SoftDeleteReferenceRangeAsync(IndicatorId, ChildId);

        Assert.That(result.NotFound, Is.True);
    }

    // â”€â”€ BulkCreateAdviceCachesAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("B")]
    public async Task BulkCreateAdviceCachesAsync_ValidEntries_AddsNormalizedEntities()
    {
        var captured = new List<LabIndicatorAdviceCache>();
        _adviceRepoMock.Setup(r => r.Add(It.IsAny<LabIndicatorAdviceCache>()))
            .Callback<LabIndicatorAdviceCache>(captured.Add);

        var result = await _service.BulkCreateAdviceCachesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorAdviceCachesRequest
            {
                AdviceCaches = new List<CreateLabIndicatorAdviceCacheRequest>
                {
                    MakeCreateAdviceRequest(LabResultStatus.High),
                    MakeCreateAdviceRequest(LabResultStatus.Low),
                },
            });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(captured, Has.Count.EqualTo(2));
        Assert.That(captured[0].DisplayTitle, Is.EqualTo("High hemoglobin"));
        Assert.That(captured[0].Summary, Is.Null);
    }

    [Test]
    [Category("B")]
    public async Task BulkCreateAdviceCachesAsync_EmptyList_ReturnsAtLeastOneRequiredError()
    {
        var result = await _service.BulkCreateAdviceCachesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorAdviceCachesRequest
            {
                AdviceCaches = new List<CreateLabIndicatorAdviceCacheRequest>(),
            });

        Assert.That(result.Errors, Does.Contain("At least one advice cache entry is required."));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateAdviceCachesAsync_UnknownStatus_ReturnsIndexPrefixedError()
    {
        var result = await _service.BulkCreateAdviceCachesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorAdviceCachesRequest
            {
                AdviceCaches = new List<CreateLabIndicatorAdviceCacheRequest>
                {
                    MakeCreateAdviceRequest(LabResultStatus.Unknown),
                },
            });

        Assert.That(result.Errors, Does.Contain("AdviceCaches[0]: Status không được là Unknown"));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateAdviceCachesAsync_StatusAlreadyPersisted_ReturnsExistsError()
    {
        _adviceRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<LabIndicatorAdviceCache, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAdvice());

        var result = await _service.BulkCreateAdviceCachesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorAdviceCachesRequest
            {
                AdviceCaches = new List<CreateLabIndicatorAdviceCacheRequest> { MakeCreateAdviceRequest() },
            });

        Assert.That(result.Errors, Does.Contain("Advice cache đã tồn tại cho status High."));
    }

    [Test]
    [Category("A")]
    public async Task BulkCreateAdviceCachesAsync_DuplicateStatusWithinBatch_ReturnsDuplicateError()
    {
        var result = await _service.BulkCreateAdviceCachesAsync(
            IndicatorId,
            new BulkCreateLabIndicatorAdviceCachesRequest
            {
                AdviceCaches = new List<CreateLabIndicatorAdviceCacheRequest>
                {
                    MakeCreateAdviceRequest(LabResultStatus.High),
                    MakeCreateAdviceRequest(LabResultStatus.High),
                },
            });

        Assert.That(result.Errors, Does.Contain("Duplicate status in request: High."));
    }

    // â”€â”€ CreateAdviceCacheAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task CreateAdviceCacheAsync_ValidRequest_PersistsEntity()
    {
        LabIndicatorAdviceCache? captured = null;
        _adviceRepoMock.Setup(r => r.Add(It.IsAny<LabIndicatorAdviceCache>()))
            .Callback<LabIndicatorAdviceCache>(e => captured = e);

        var result = await _service.CreateAdviceCacheAsync(IndicatorId, MakeCreateAdviceRequest());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(captured!.Status, Is.EqualTo(LabResultStatus.High));
        Assert.That(captured.SeverityLevel, Is.EqualTo(LabAdviceSeverityLevel.Warning));
    }

    [Test]
    [Category("A")]
    public async Task CreateAdviceCacheAsync_NullRequest_ReturnsRequiredError()
    {
        var result = await _service.CreateAdviceCacheAsync(IndicatorId, null!);

        Assert.That(result.Errors, Does.Contain("Request advice cache là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task CreateAdviceCacheAsync_UnknownStatus_ReturnsStatusError()
    {
        var result = await _service.CreateAdviceCacheAsync(
            IndicatorId, MakeCreateAdviceRequest(LabResultStatus.Unknown));

        Assert.That(result.Errors, Does.Contain("Status không được là Unknown"));
    }

    [Test]
    [Category("A")]
    public async Task CreateAdviceCacheAsync_StatusAlreadyExists_ReturnsExistsError()
    {
        _adviceRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<LabIndicatorAdviceCache, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAdvice());

        var result = await _service.CreateAdviceCacheAsync(IndicatorId, MakeCreateAdviceRequest());

        Assert.That(result.Errors, Does.Contain("Advice cache đã tồn tại cho status High."));
    }

    // â”€â”€ UpdateAdviceCacheAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task UpdateAdviceCacheAsync_ValidRequest_AppliesNewValues()
    {
        var advice = MakeAdvice();
        _adviceRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>())).ReturnsAsync(advice);

        var result = await _service.UpdateAdviceCacheAsync(IndicatorId, ChildId, new UpdateLabIndicatorAdviceCacheRequest
        {
            Status = LabResultStatus.Low,
            DisplayTitle = "  Low hemoglobin  ",
            SeverityLevel = LabAdviceSeverityLevel.Critical,
        });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(advice.Status, Is.EqualTo(LabResultStatus.Low));
        Assert.That(advice.DisplayTitle, Is.EqualTo("Low hemoglobin"));
    }

    [Test]
    [Category("B")]
    public async Task UpdateAdviceCacheAsync_EmptyCacheId_ReturnsInvalidIdError()
    {
        var result = await _service.UpdateAdviceCacheAsync(
            IndicatorId,
            Guid.Empty,
            new UpdateLabIndicatorAdviceCacheRequest { Status = LabResultStatus.High });

        Assert.That(result.Errors, Does.Contain("Id advice cache không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateAdviceCacheAsync_NullRequest_ReturnsRequiredError()
    {
        var result = await _service.UpdateAdviceCacheAsync(IndicatorId, ChildId, null!);

        Assert.That(result.Errors, Does.Contain("Request advice cache là bắt buộc"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateAdviceCacheAsync_UnknownStatus_ReturnsStatusError()
    {
        var result = await _service.UpdateAdviceCacheAsync(
            IndicatorId,
            ChildId,
            new UpdateLabIndicatorAdviceCacheRequest { Status = LabResultStatus.Unknown });

        Assert.That(result.Errors, Does.Contain("Status không được là Unknown"));
    }

    [Test]
    [Category("A")]
    public async Task UpdateAdviceCacheAsync_CacheBelongsToAnotherIndicator_ReturnsNotFound()
    {
        _adviceRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAdvice(indicatorId: OtherIndicatorId));

        var result = await _service.UpdateAdviceCacheAsync(
            IndicatorId,
            ChildId,
            new UpdateLabIndicatorAdviceCacheRequest { Status = LabResultStatus.High });

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task UpdateAdviceCacheAsync_StatusTakenBySiblingCache_ReturnsExistsError()
    {
        _adviceRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAdvice());
        _adviceRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<LabIndicatorAdviceCache, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAdvice(id: Guid.NewGuid(), status: LabResultStatus.Low));

        var result = await _service.UpdateAdviceCacheAsync(
            IndicatorId,
            ChildId,
            new UpdateLabIndicatorAdviceCacheRequest { Status = LabResultStatus.Low });

        Assert.That(result.Errors, Does.Contain("Advice cache đã tồn tại cho status Low."));
    }

    // â”€â”€ SoftDeleteAdviceCacheAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    [Category("N")]
    public async Task SoftDeleteAdviceCacheAsync_Found_MarksCacheDeleted()
    {
        var advice = MakeAdvice();
        _adviceRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>())).ReturnsAsync(advice);

        var result = await _service.SoftDeleteAdviceCacheAsync(IndicatorId, ChildId);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(advice.IsDeleted, Is.True);
        Assert.That(advice.DeletedAt, Is.Not.Null);
    }

    [Test]
    [Category("B")]
    public async Task SoftDeleteAdviceCacheAsync_EmptyCacheId_ReturnsInvalidIdError()
    {
        var result = await _service.SoftDeleteAdviceCacheAsync(IndicatorId, Guid.Empty);

        Assert.That(result.Errors, Does.Contain("Id advice cache không hợp lệ"));
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteAdviceCacheAsync_CacheMissing_ReturnsNotFound()
    {
        _adviceRepoMock.Setup(r => r.GetByIdAsync(ChildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabIndicatorAdviceCache?)null);

        var result = await _service.SoftDeleteAdviceCacheAsync(IndicatorId, ChildId);

        Assert.That(result.NotFound, Is.True);
    }

    [Test]
    [Category("A")]
    public async Task SoftDeleteAdviceCacheAsync_IndicatorMissing_ReturnsNotFound()
    {
        IndicatorNotFound();

        var result = await _service.SoftDeleteAdviceCacheAsync(IndicatorId, ChildId);

        Assert.That(result.NotFound, Is.True);
    }
}
