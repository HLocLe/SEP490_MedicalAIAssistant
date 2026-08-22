using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.RecoveryPlanTemplates;

public sealed class CreateRecoveryPlanTemplateRequest
{
    public string TemplateName { get; set; } = string.Empty;
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public string? Summary { get; set; }
    public string? RecheckInstruction { get; set; }
}

public sealed class UpdateRecoveryPlanTemplateRequest
{
    public string TemplateName { get; set; } = string.Empty;
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public string? Summary { get; set; }
    public string? RecheckInstruction { get; set; }
}

public sealed class CreateRecoveryPlanFromTemplateRequest
{
    public Guid TemplateId { get; set; }
}

public sealed class RecoveryPlanTemplateSummaryResponse
{
    public Guid Id { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public bool IsComplete { get; set; }
    public int PhaseCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class RecoveryPlanTemplateDetailResponse
{
    public Guid Id { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public string? Summary { get; set; }
    public string? RecheckInstruction { get; set; }
    public bool IsComplete { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<RecoveryPlanTemplatePhaseResponse> Phases { get; set; } =
        Array.Empty<RecoveryPlanTemplatePhaseResponse>();
}

public sealed class RecoveryPlanTemplatePhaseResponse
{
    public Guid Id { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public int StartDay { get; set; }
    public int EndDay { get; set; }
    public decimal? SleepAndRestHoursPerDay { get; set; }
    public string? Instruction { get; set; }
    public int SortOrder { get; set; }
    public IReadOnlyList<RecoveryPlanTemplateNutrientTargetResponse> NutrientTargets {
        get;
        set;
    } = Array.Empty<RecoveryPlanTemplateNutrientTargetResponse>();
}

public sealed class RecoveryPlanTemplateNutrientTargetResponse
{
    public Guid Id { get; set; }
    public string NutrientName { get; set; } = string.Empty;
    public decimal AmountPerDay { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Instruction { get; set; }
    public int SortOrder { get; set; }
    public IReadOnlyList<RecoveryPlanTemplateFoodSourceResponse> FoodSources { get; set; } =
        Array.Empty<RecoveryPlanTemplateFoodSourceResponse>();
}

public sealed class RecoveryPlanTemplateFoodSourceResponse
{
    public Guid Id { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string? SuggestedServing { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}
