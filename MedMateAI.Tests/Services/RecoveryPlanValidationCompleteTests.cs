using MedMateAI.Application.Models;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanValidationCompleteTests
{
    // ── helpers ────────────────────────────────────────────────────────────────

    private static RecoveryPlanFoodSource MakeFood(int sortOrder = 0, string name = "Chicken") =>
        new() { Id = Guid.NewGuid(), FoodName = name, SortOrder = sortOrder };

    private static RecoveryPlanNutrientTarget MakeNutrient(
        int sortOrder = 0,
        List<RecoveryPlanFoodSource>? foods = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            NutrientName = "Protein",
            AmountPerDay = 50m,
            Unit = "g",
            SortOrder = sortOrder,
            FoodSources = foods ?? new List<RecoveryPlanFoodSource> { MakeFood() }
        };

    private static RecoveryPlanPhase MakePhase(
        int startDay, int endDay, int sortOrder = 0,
        List<RecoveryPlanNutrientTarget>? nutrients = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            PhaseName = "Phase 1",
            StartDay = startDay,
            EndDay = endDay,
            SleepAndRestHoursPerDay = 10m,
            SortOrder = sortOrder,
            NutrientTargets = nutrients ?? new List<RecoveryPlanNutrientTarget> { MakeNutrient() }
        };

    private static RecoveryPlan MakeValidPlan(int durationDays = 7) =>
        new()
        {
            Id = Guid.NewGuid(),
            PlanName = "Recovery Plan",
            Summary = "Get better soon",
            RecheckInstruction = "Check weekly",
            DurationDays = durationDays,
            Phases = new List<RecoveryPlanPhase> { MakePhase(1, durationDays) }
        };

    // ── plan-level header validation ───────────────────────────────────────────

    [Test]
    [Category("N")]
    public void ValidateCompletePlan_ValidPlan_ReturnsNone()
    {
        var plan = MakeValidPlan();
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.None));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_NullPlanName_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        plan.PlanName = null;
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_WhitespacePlanName_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        plan.PlanName = "   ";
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_NullSummary_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        plan.Summary = null;
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_NullRecheckInstruction_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        plan.RecheckInstruction = null;
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_DurationZero_ReturnsInvalidPlanStructure()
    {
        var plan = MakeValidPlan(7);
        plan.DurationDays = 0;
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_DurationExceeds365_ReturnsInvalidPlanStructure()
    {
        var plan = MakeValidPlan(7);
        plan.DurationDays = 366;
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_NoPhases_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        plan.Phases = new List<RecoveryPlanPhase>();
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_AllPhasesDeleted_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        foreach (var phase in plan.Phases) phase.IsDeleted = true;
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("A")]
    public void ValidateCompletePlan_DuplicatePhaseSortOrder_ReturnsInvalidPlanStructure()
    {
        var plan = MakeValidPlan(14);
        plan.Phases = new List<RecoveryPlanPhase>
        {
            MakePhase(1, 7, sortOrder: 0),
            MakePhase(8, 14, sortOrder: 0)    // duplicate sort order
        };
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    [Test]
    [Category("A")]
    public void ValidateCompletePlan_GapBetweenPhases_ReturnsInvalidPlanStructure()
    {
        var plan = MakeValidPlan(14);
        plan.Phases = new List<RecoveryPlanPhase>
        {
            MakePhase(1, 6, sortOrder: 0),
            MakePhase(8, 14, sortOrder: 1)    // gap on day 7
        };
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    [Test]
    [Category("A")]
    public void ValidateCompletePlan_PhasesNotCoveringFullDuration_ReturnsInvalidPlanStructure()
    {
        var plan = MakeValidPlan(14);
        plan.Phases = new List<RecoveryPlanPhase>
        {
            MakePhase(1, 13, sortOrder: 0)    // misses day 14
        };
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    [Test]
    [Category("N")]
    public void ValidateCompletePlan_MultipleValidPhases_ReturnsNone()
    {
        var plan = MakeValidPlan(14);
        plan.Phases = new List<RecoveryPlanPhase>
        {
            MakePhase(1, 7, sortOrder: 0),
            MakePhase(8, 14, sortOrder: 1)
        };
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.None));
    }

    // ── phase-level validation ─────────────────────────────────────────────────

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_PhaseHasNoSleepAndRestHours_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        plan.Phases.First().SleepAndRestHoursPerDay = null;
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_PhaseSleepAndRestExceeds24_ReturnsInvalidPlanStructure()
    {
        var plan = MakeValidPlan();
        plan.Phases.First().SleepAndRestHoursPerDay = 25m;
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_PhaseNoNutrients_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        plan.Phases.First().NutrientTargets = new List<RecoveryPlanNutrientTarget>();
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_AllNutrientsDeleted_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        foreach (var n in plan.Phases.First().NutrientTargets) n.IsDeleted = true;
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("A")]
    public void ValidateCompletePlan_DuplicateNutrientSortOrder_ReturnsInvalidPlanStructure()
    {
        var plan = MakeValidPlan();
        plan.Phases.First().NutrientTargets = new List<RecoveryPlanNutrientTarget>
        {
            MakeNutrient(sortOrder: 0),
            MakeNutrient(sortOrder: 0)
        };
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }

    // ── nutrient/food-level validation ─────────────────────────────────────────

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_NutrientNoFoods_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        plan.Phases.First().NutrientTargets = new List<RecoveryPlanNutrientTarget>
        {
            MakeNutrient(foods: new List<RecoveryPlanFoodSource>())
        };
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_AllFoodsDeleted_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        foreach (var food in plan.Phases.First().NutrientTargets.First().FoodSources) food.IsDeleted = true;
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("B")]
    public void ValidateCompletePlan_FoodEmptyName_ReturnsIncomplete()
    {
        var plan = MakeValidPlan();
        plan.Phases.First().NutrientTargets = new List<RecoveryPlanNutrientTarget>
        {
            MakeNutrient(foods: new List<RecoveryPlanFoodSource> { MakeFood(name: "   ") })
        };
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.RecoveryPlanIncomplete));
    }

    [Test]
    [Category("A")]
    public void ValidateCompletePlan_DuplicateFoodSortOrder_ReturnsInvalidPlanStructure()
    {
        var plan = MakeValidPlan();
        plan.Phases.First().NutrientTargets = new List<RecoveryPlanNutrientTarget>
        {
            MakeNutrient(foods: new List<RecoveryPlanFoodSource>
            {
                MakeFood(sortOrder: 0),
                MakeFood(sortOrder: 0)
            })
        };
        Assert.That(RecoveryPlanValidation.ValidateCompletePlan(plan), Is.EqualTo(RecoveryPlanErrorCode.InvalidPlanStructure));
    }
}
