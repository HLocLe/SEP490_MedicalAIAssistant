using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.Models;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanDraftMutationsTests
{
    private static readonly DateTime UtcNow = new(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

    // ── builders ───────────────────────────────────────────────────────────────

    private static RecoveryPlanFoodSource MakeFood(int sortOrder = 0) =>
        new() { Id = Guid.NewGuid(), FoodName = "Chicken", SortOrder = sortOrder };

    private static RecoveryPlanNutrientTarget MakeNutrient(int sortOrder = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            NutrientName = "Protein",
            AmountPerDay = 50m,
            Unit = "g",
            SortOrder = sortOrder,
            FoodSources = new List<RecoveryPlanFoodSource> { MakeFood() }
        };

    private static RecoveryPlanPhase MakePhase(
        int startDay, int endDay, int sortOrder = 0,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            PhaseName = "Phase 1",
            StartDay = startDay,
            EndDay = endDay,
            SleepAndRestHoursPerDay = 10m,
            SortOrder = sortOrder,
            NutrientTargets = new List<RecoveryPlanNutrientTarget> { MakeNutrient() }
        };

    private static RecoveryPlan MakePlan(int durationDays = 30) =>
        new()
        {
            Id = Guid.NewGuid(),
            RecoveryPlanRequestId = Guid.NewGuid(),   // required by RecoveryPlanMapping.ToDetail
            PlanName = "Plan",
            Summary = "Summary",
            DurationDays = durationDays,
            Phases = new List<RecoveryPlanPhase>()
        };

    // ════════════════════════════════════════════════════════════════
    // UpdateHeader
    // ════════════════════════════════════════════════════════════════

    [Test]
    [Category("N")]
    public void UpdateHeader_ValidRequest_UpdatesPlanAndReturnsSuccess()
    {
        var plan = MakePlan();
        var req = new UpdateRecoveryPlanDraftRequest
        {
            PlanName = "New Plan",
            Summary = "New summary",
            DurationDays = 14,
            RecheckInstruction = "Monthly"
        };

        var result = RecoveryPlanDraftMutations.UpdateHeader(plan, req, UtcNow);

        Assert.That(result.Success, Is.True);
        Assert.That(plan.PlanName, Is.EqualTo("New Plan"));
        Assert.That(plan.DurationDays, Is.EqualTo(14));
        Assert.That(plan.UpdatedAt, Is.EqualTo(UtcNow));
    }

    [Test]
    [Category("B")]
    public void UpdateHeader_EmptyPlanName_ReturnsFailInvalidRequest()
    {
        var plan = MakePlan();
        var req = new UpdateRecoveryPlanDraftRequest { PlanName = "", DurationDays = 14 };

        var result = RecoveryPlanDraftMutations.UpdateHeader(plan, req, UtcNow);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    [Category("B")]
    public void UpdateHeader_PhaseEndDayExceedsNewDuration_ReturnsFailInvalidPlanStructure()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        plan.Phases.Add(MakePhase(1, 20, id: phaseId));  // EndDay=20
        var req = new UpdateRecoveryPlanDraftRequest
        {
            PlanName = "Plan",
            DurationDays = 10    // new duration < existing phase EndDay
        };

        var result = RecoveryPlanDraftMutations.UpdateHeader(plan, req, UtcNow);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    [Test]
    [Category("B")]
    public void UpdateHeader_SummaryNormalized_NullForWhitespace()
    {
        var plan = MakePlan();
        var req = new UpdateRecoveryPlanDraftRequest
        {
            PlanName = "Plan",
            Summary = "   ",
            DurationDays = 30
        };

        var result = RecoveryPlanDraftMutations.UpdateHeader(plan, req, UtcNow);

        Assert.That(result.Success, Is.True);
        Assert.That(plan.Summary, Is.Null);
    }

    // ════════════════════════════════════════════════════════════════
    // DeletePlan
    // ════════════════════════════════════════════════════════════════

    [Test]
    [Category("N")]
    public void DeletePlan_SoftDeletesPlanAndAllPhases()
    {
        var plan = MakePlan();
        var phase = MakePhase(1, 7);
        plan.Phases.Add(phase);

        var result = RecoveryPlanDraftMutations.DeletePlan(plan, UtcNow);

        Assert.That(result.Success, Is.True);
        Assert.That(plan.IsDeleted, Is.True);
        Assert.That(plan.DeletedAt, Is.EqualTo(UtcNow));
        Assert.That(phase.IsDeleted, Is.True);
    }

    [Test]
    [Category("N")]
    public void DeletePlan_CascadeDeletesNutrientsAndFoods()
    {
        var plan = MakePlan();
        var food = MakeFood();
        var nutrient = new RecoveryPlanNutrientTarget
        {
            Id = Guid.NewGuid(), NutrientName = "Protein", AmountPerDay = 50m,
            Unit = "g", SortOrder = 0,
            FoodSources = new List<RecoveryPlanFoodSource> { food }
        };
        var phase = new RecoveryPlanPhase
        {
            Id = Guid.NewGuid(), PhaseName = "P1", StartDay = 1, EndDay = 7,
            SleepAndRestHoursPerDay = 10m, SortOrder = 0,
            NutrientTargets = new List<RecoveryPlanNutrientTarget> { nutrient }
        };
        plan.Phases.Add(phase);

        RecoveryPlanDraftMutations.DeletePlan(plan, UtcNow);

        Assert.That(nutrient.IsDeleted, Is.True);
        Assert.That(food.IsDeleted, Is.True);
    }

    // ════════════════════════════════════════════════════════════════
    // CreatePhase
    // ════════════════════════════════════════════════════════════════

    [Test]
    [Category("N")]
    public void CreatePhase_ValidRequest_AddsPhaseAndReturnsSuccess()
    {
        var plan = MakePlan(30);
        var req = new UpsertRecoveryPlanPhaseRequest
        {
            PhaseName = "Rest Phase", StartDay = 1, EndDay = 7,
            SleepAndRestHoursPerDay = 10m, SortOrder = 0
        };

        var result = RecoveryPlanDraftMutations.CreatePhase(plan, req, UtcNow);

        Assert.That(result.Success, Is.True);
        Assert.That(plan.Phases, Has.Count.EqualTo(1));
        Assert.That(result.Data!.PhaseName, Is.EqualTo("Rest Phase"));
    }

    [Test]
    [Category("B")]
    public void CreatePhase_InvalidPhase_EndBeforeStart_ReturnsFailInvalidRequest()
    {
        var plan = MakePlan(30);
        var req = new UpsertRecoveryPlanPhaseRequest
        {
            PhaseName = "Phase", StartDay = 10, EndDay = 5,
            SleepAndRestHoursPerDay = 10m, SortOrder = 0
        };

        var result = RecoveryPlanDraftMutations.CreatePhase(plan, req, UtcNow);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    [Category("A")]
    public void CreatePhase_SortOrderConflict_ReturnsFailInvalidPlanStructure()
    {
        var plan = MakePlan(30);
        plan.Phases.Add(MakePhase(1, 7, sortOrder: 0));
        var req = new UpsertRecoveryPlanPhaseRequest
        {
            PhaseName = "Phase 2", StartDay = 8, EndDay = 14,
            SleepAndRestHoursPerDay = 10m, SortOrder = 0    // duplicate
        };

        var result = RecoveryPlanDraftMutations.CreatePhase(plan, req, UtcNow);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    [Test]
    [Category("A")]
    public void CreatePhase_DayRangeOverlap_ReturnsFailInvalidPlanStructure()
    {
        var plan = MakePlan(30);
        plan.Phases.Add(MakePhase(1, 10, sortOrder: 0));
        var req = new UpsertRecoveryPlanPhaseRequest
        {
            PhaseName = "Phase 2", StartDay = 5, EndDay = 15,    // overlaps 5-10
            SleepAndRestHoursPerDay = 10m, SortOrder = 1
        };

        var result = RecoveryPlanDraftMutations.CreatePhase(plan, req, UtcNow);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    // ════════════════════════════════════════════════════════════════
    // UpdatePhase
    // ════════════════════════════════════════════════════════════════

    [Test]
    [Category("A")]
    public void UpdatePhase_PhaseNotFound_ReturnsFailNotFound()
    {
        var plan = MakePlan(30);
        var req = new UpsertRecoveryPlanPhaseRequest
        {
            PhaseName = "X", StartDay = 1, EndDay = 7,
            SleepAndRestHoursPerDay = 10m, SortOrder = 0
        };

        var result = RecoveryPlanDraftMutations.UpdatePhase(plan, Guid.NewGuid(), req, UtcNow);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public void UpdatePhase_ValidUpdate_ChangesPhaseValues()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        plan.Phases.Add(MakePhase(1, 7, sortOrder: 0, id: phaseId));
        var req = new UpsertRecoveryPlanPhaseRequest
        {
            PhaseName = "Updated Phase", StartDay = 1, EndDay = 10,
            SleepAndRestHoursPerDay = 10m, SortOrder = 0
        };

        var result = RecoveryPlanDraftMutations.UpdatePhase(plan, phaseId, req, UtcNow);

        Assert.That(result.Success, Is.True);
        var phase = plan.Phases.First(p => p.Id == phaseId);
        Assert.That(phase.PhaseName, Is.EqualTo("Updated Phase"));
        Assert.That(phase.EndDay, Is.EqualTo(10));
    }

    // ════════════════════════════════════════════════════════════════
    // DeletePhase
    // ════════════════════════════════════════════════════════════════

    [Test]
    [Category("A")]
    public void DeletePhase_PhaseNotFound_ReturnsFailNotFound()
    {
        var plan = MakePlan(30);
        var result = RecoveryPlanDraftMutations.DeletePhase(plan, Guid.NewGuid(), UtcNow);
        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public void DeletePhase_ValidDelete_SoftDeletesPhase()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        plan.Phases.Add(MakePhase(1, 7, id: phaseId));

        var result = RecoveryPlanDraftMutations.DeletePhase(plan, phaseId, UtcNow);

        Assert.That(result.Success, Is.True);
        Assert.That(plan.Phases.First(p => p.Id == phaseId).IsDeleted, Is.True);
    }

    // ════════════════════════════════════════════════════════════════
    // CreateNutrient
    // ════════════════════════════════════════════════════════════════

    [Test]
    [Category("A")]
    public void CreateNutrient_PhaseNotFound_ReturnsFailNotFound()
    {
        var plan = MakePlan(30);
        var req = new UpsertRecoveryPlanNutrientTargetRequest
        {
            NutrientName = "Protein", AmountPerDay = 50m, Unit = "g", SortOrder = 0
        };

        var result = RecoveryPlanDraftMutations.CreateNutrient(plan, Guid.NewGuid(), req, UtcNow);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public void CreateNutrient_Valid_AddsNutrientToPhase()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        var phase = MakePhase(1, 7, id: phaseId);
        phase.NutrientTargets.Clear();
        plan.Phases.Add(phase);

        var req = new UpsertRecoveryPlanNutrientTargetRequest
        {
            NutrientName = "Protein", AmountPerDay = 50m, Unit = "g", SortOrder = 0
        };

        var result = RecoveryPlanDraftMutations.CreateNutrient(plan, phaseId, req, UtcNow);

        Assert.That(result.Success, Is.True);
        Assert.That(phase.NutrientTargets, Has.Count.EqualTo(1));
        Assert.That(result.Data!.NutrientName, Is.EqualTo("Protein"));
    }

    [Test]
    [Category("A")]
    public void CreateNutrient_SortOrderConflict_ReturnsFailInvalidPlanStructure()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        var phase = MakePhase(1, 7, id: phaseId);
        plan.Phases.Add(phase);   // phase already has a nutrient with SortOrder=0

        var req = new UpsertRecoveryPlanNutrientTargetRequest
        {
            NutrientName = "Fat", AmountPerDay = 20m, Unit = "g", SortOrder = 0    // duplicate
        };

        var result = RecoveryPlanDraftMutations.CreateNutrient(plan, phaseId, req, UtcNow);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    // ════════════════════════════════════════════════════════════════
    // UpdateNutrient
    // ════════════════════════════════════════════════════════════════

    [Test]
    [Category("A")]
    public void UpdateNutrient_NutrientNotFound_ReturnsFailNotFound()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        plan.Phases.Add(MakePhase(1, 7, id: phaseId));

        var req = new UpsertRecoveryPlanNutrientTargetRequest
        {
            NutrientName = "Protein", AmountPerDay = 50m, Unit = "g", SortOrder = 0
        };

        var result = RecoveryPlanDraftMutations.UpdateNutrient(plan, phaseId, Guid.NewGuid(), req, UtcNow);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public void UpdateNutrient_Valid_UpdatesNutrientValues()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        var phase = MakePhase(1, 7, id: phaseId);
        var nutrientId = phase.NutrientTargets.First().Id;
        plan.Phases.Add(phase);

        var req = new UpsertRecoveryPlanNutrientTargetRequest
        {
            NutrientName = "Fat", AmountPerDay = 30m, Unit = "g", SortOrder = 0
        };

        var result = RecoveryPlanDraftMutations.UpdateNutrient(plan, phaseId, nutrientId, req, UtcNow);

        Assert.That(result.Success, Is.True);
        Assert.That(phase.NutrientTargets.First().NutrientName, Is.EqualTo("Fat"));
        Assert.That(phase.NutrientTargets.First().AmountPerDay, Is.EqualTo(30m));
    }

    // ════════════════════════════════════════════════════════════════
    // DeleteNutrient
    // ════════════════════════════════════════════════════════════════

    [Test]
    [Category("A")]
    public void DeleteNutrient_NotFound_ReturnsFailNotFound()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        plan.Phases.Add(MakePhase(1, 7, id: phaseId));

        var result = RecoveryPlanDraftMutations.DeleteNutrient(plan, phaseId, Guid.NewGuid(), UtcNow);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public void DeleteNutrient_Valid_SoftDeletesNutrientAndFoods()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        var phase = MakePhase(1, 7, id: phaseId);
        var nutrientId = phase.NutrientTargets.First().Id;
        var food = phase.NutrientTargets.First().FoodSources.First();
        plan.Phases.Add(phase);

        var result = RecoveryPlanDraftMutations.DeleteNutrient(plan, phaseId, nutrientId, UtcNow);

        Assert.That(result.Success, Is.True);
        Assert.That(phase.NutrientTargets.First().IsDeleted, Is.True);
        Assert.That(food.IsDeleted, Is.True);
    }

    // ════════════════════════════════════════════════════════════════
    // CreateFood
    // ════════════════════════════════════════════════════════════════

    [Test]
    [Category("A")]
    public void CreateFood_NutrientNotFound_ReturnsFailNotFound()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        plan.Phases.Add(MakePhase(1, 7, id: phaseId));

        var req = new UpsertRecoveryPlanFoodSourceRequest { FoodName = "Rice", SortOrder = 1 };

        var result = RecoveryPlanDraftMutations.CreateFood(plan, phaseId, Guid.NewGuid(), req, UtcNow);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public void CreateFood_Valid_AddsFoodToNutrient()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        var phase = MakePhase(1, 7, id: phaseId);
        var nutrientId = phase.NutrientTargets.First().Id;
        plan.Phases.Add(phase);

        var req = new UpsertRecoveryPlanFoodSourceRequest { FoodName = "Rice", SortOrder = 1 };

        var result = RecoveryPlanDraftMutations.CreateFood(plan, phaseId, nutrientId, req, UtcNow);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.FoodName, Is.EqualTo("Rice"));
    }

    [Test]
    [Category("A")]
    public void CreateFood_SortOrderConflict_ReturnsFailInvalidPlanStructure()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        var phase = MakePhase(1, 7, id: phaseId);
        var nutrientId = phase.NutrientTargets.First().Id;
        plan.Phases.Add(phase);   // food already has SortOrder=0

        var req = new UpsertRecoveryPlanFoodSourceRequest { FoodName = "Rice", SortOrder = 0 };

        var result = RecoveryPlanDraftMutations.CreateFood(plan, phaseId, nutrientId, req, UtcNow);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    // ════════════════════════════════════════════════════════════════
    // UpdateFood
    // ════════════════════════════════════════════════════════════════

    [Test]
    [Category("A")]
    public void UpdateFood_FoodNotFound_ReturnsFailNotFound()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        var phase = MakePhase(1, 7, id: phaseId);
        var nutrientId = phase.NutrientTargets.First().Id;
        plan.Phases.Add(phase);

        var req = new UpsertRecoveryPlanFoodSourceRequest { FoodName = "Rice", SortOrder = 0 };

        var result = RecoveryPlanDraftMutations.UpdateFood(plan, phaseId, nutrientId, Guid.NewGuid(), req, UtcNow);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public void UpdateFood_Valid_UpdatesFoodName()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        var phase = MakePhase(1, 7, id: phaseId);
        var nutrientId = phase.NutrientTargets.First().Id;
        var foodId = phase.NutrientTargets.First().FoodSources.First().Id;
        plan.Phases.Add(phase);

        var req = new UpsertRecoveryPlanFoodSourceRequest { FoodName = "Brown Rice", SortOrder = 0 };

        var result = RecoveryPlanDraftMutations.UpdateFood(plan, phaseId, nutrientId, foodId, req, UtcNow);

        Assert.That(result.Success, Is.True);
        Assert.That(phase.NutrientTargets.First().FoodSources.First().FoodName, Is.EqualTo("Brown Rice"));
    }

    // ════════════════════════════════════════════════════════════════
    // DeleteFood
    // ════════════════════════════════════════════════════════════════

    [Test]
    [Category("A")]
    public void DeleteFood_FoodNotFound_ReturnsFailNotFound()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        var phase = MakePhase(1, 7, id: phaseId);
        var nutrientId = phase.NutrientTargets.First().Id;
        plan.Phases.Add(phase);

        var result = RecoveryPlanDraftMutations.DeleteFood(plan, phaseId, nutrientId, Guid.NewGuid(), UtcNow);

        Assert.That(result.Error, Is.EqualTo(RecoveryPlanErrorCode.NotFound));
    }

    [Test]
    [Category("N")]
    public void DeleteFood_Valid_SoftDeletesFood()
    {
        var plan = MakePlan(30);
        var phaseId = Guid.NewGuid();
        var phase = MakePhase(1, 7, id: phaseId);
        var nutrientId = phase.NutrientTargets.First().Id;
        var foodId = phase.NutrientTargets.First().FoodSources.First().Id;
        plan.Phases.Add(phase);

        var result = RecoveryPlanDraftMutations.DeleteFood(plan, phaseId, nutrientId, foodId, UtcNow);

        Assert.That(result.Success, Is.True);
        Assert.That(phase.NutrientTargets.First().FoodSources.First().IsDeleted, Is.True);
    }
}
