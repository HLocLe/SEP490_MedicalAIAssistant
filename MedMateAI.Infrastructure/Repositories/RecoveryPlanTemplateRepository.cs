using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class RecoveryPlanTemplateRepository : IRecoveryPlanTemplateRepository
{
    private readonly ApplicationDbContext _context;

    public RecoveryPlanTemplateRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<RecoveryPlanTemplate>> GetPagedAsync(
        Guid doctorId,
        int pageNumber,
        int pageSize,
        RecoveryPlanDiseaseGroup? diseaseGroup,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RecoveryPlanTemplates
            .AsNoTracking()
            .Where(template => template.DoctorId == doctorId && !template.IsDeleted);

        if (diseaseGroup.HasValue)
        {
            query = query.Where(template => template.DiseaseGroup == diseaseGroup.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(template =>
                template.TemplateName.ToLower().Contains(normalizedSearch)
                || template.PlanName.ToLower().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageIds = await query
            .OrderByDescending(template => template.UpdatedAt ?? template.CreatedAt)
            .ThenByDescending(template => template.CreatedAt)
            .ThenBy(template => template.Id)
            .Select(template => template.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (pageIds.Count == 0)
        {
            return ToPage(Array.Empty<RecoveryPlanTemplate>(), pageNumber, pageSize, totalCount);
        }

        var templates = await DetailQuery(_context.RecoveryPlanTemplates.AsNoTracking())
            .AsSplitQuery()
            .Where(template =>
                template.DoctorId == doctorId
                && !template.IsDeleted
                && pageIds.Contains(template.Id))
            .ToListAsync(cancellationToken);
        var pageOrder = pageIds
            .Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index);
        var orderedTemplates = templates
            .OrderBy(template => pageOrder[template.Id])
            .ToList();

        return ToPage(orderedTemplates, pageNumber, pageSize, totalCount);
    }

    public Task<RecoveryPlanTemplate?> GetDetailAsync(
        Guid doctorId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        return DetailQuery(_context.RecoveryPlanTemplates.AsNoTracking())
            .FirstOrDefaultAsync(
                template =>
                    template.Id == templateId
                    && template.DoctorId == doctorId
                    && !template.IsDeleted,
                cancellationToken);
    }

    public async Task<RecoveryPlanTemplate?> GetByIdForUpdateAsync(
        Guid doctorId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        var templates = await _context.RecoveryPlanTemplates
            .FromSqlInterpolated($"""
                SELECT *
                FROM "RecoveryPlanTemplate"
                WHERE "RecoveryPlanTemplateId" = {templateId}
                  AND "DoctorId" = {doctorId}
                  AND "IsDeleted" = FALSE
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        return templates.SingleOrDefault();
    }

    public Task<RecoveryPlanTemplate?> GetTrackedDetailAsync(
        Guid doctorId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        return DetailQuery(_context.RecoveryPlanTemplates.AsTracking())
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                template =>
                    template.Id == templateId
                    && template.DoctorId == doctorId
                    && !template.IsDeleted,
                cancellationToken);
    }

    public void Add(RecoveryPlanTemplate template)
    {
        _context.RecoveryPlanTemplates.Add(template);
    }

    private static IQueryable<RecoveryPlanTemplate> DetailQuery(
        IQueryable<RecoveryPlanTemplate> query)
    {
        return query
            .Include(template => template.Phases.Where(phase => !phase.IsDeleted))
            .ThenInclude(phase =>
                phase.NutrientTargets.Where(nutrient => !nutrient.IsDeleted))
            .ThenInclude(nutrient =>
                nutrient.FoodSources.Where(food => !food.IsDeleted));
    }

    private static PagedResult<RecoveryPlanTemplate> ToPage(
        IReadOnlyList<RecoveryPlanTemplate> templates,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        return new PagedResult<RecoveryPlanTemplate>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = templates
        };
    }

    private void EnsureActiveTransaction()
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Recovery plan template locking and tracked aggregate writes require an active database transaction.");
        }
    }
}
