using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.Models;
using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanValidationTests
{
    // ──────────────────────────────────────────────
    // ValidateDraftHeader
    // ──────────────────────────────────────────────

    [Test]
    public void ValidateDraftHeader_ValidInputs_ReturnsNone()
    {
        var result = RecoveryPlanValidation.ValidateDraftHeader("Plan A", "Summary", 30, null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.None));
    }

    [Test]
    public void ValidateDraftHeader_EmptyPlanName_ReturnsInvalidRequest()
    {
        var result = RecoveryPlanValidation.ValidateDraftHeader("", null, 30, null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidateDraftHeader_PlanNameExceedsMax_ReturnsInvalidRequest()
    {
        var longName = new string('x', RecoveryPlanValidation.MaximumPlanNameLength + 1);
        var result = RecoveryPlanValidation.ValidateDraftHeader(longName, null, 30, null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidateDraftHeader_DurationZero_ReturnsInvalidRequest()
    {
        var result = RecoveryPlanValidation.ValidateDraftHeader("Plan", null, 0, null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidateDraftHeader_DurationExceeds365_ReturnsInvalidRequest()
    {
        var result = RecoveryPlanValidation.ValidateDraftHeader("Plan", null, 366, null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidateDraftHeader_DurationAt365_ReturnsNone()
    {
        var result = RecoveryPlanValidation.ValidateDraftHeader("Plan", null, 365, null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.None));
    }

    [Test]
    public void ValidateDraftHeader_SummaryExceedsMax_ReturnsInvalidRequest()
    {
        var longSummary = new string('x', RecoveryPlanValidation.MaximumSummaryLength + 1);
        var result = RecoveryPlanValidation.ValidateDraftHeader("Plan", longSummary, 30, null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidateDraftHeader_RecheckInstructionExceedsMax_ReturnsInvalidRequest()
    {
        var longRecheck = new string('x', RecoveryPlanValidation.MaximumRecheckInstructionLength + 1);
        var result = RecoveryPlanValidation.ValidateDraftHeader("Plan", null, 30, longRecheck);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    // ──────────────────────────────────────────────
    // ValidatePhase
    // ──────────────────────────────────────────────

    private static UpsertRecoveryPlanPhaseRequest ValidPhaseRequest() => new()
    {
        PhaseName = "Phase 1",
        StartDay = 1,
        EndDay = 7,
        SleepAndRestHoursPerDay = 10,
        SortOrder = 0
    };

    [Test]
    public void ValidatePhase_ValidInputs_ReturnsNone()
    {
        var req = ValidPhaseRequest();
        var result = RecoveryPlanValidation.ValidatePhase(req, "Phase 1", null, 30);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.None));
    }

    [Test]
    public void ValidatePhase_EmptyPhaseName_ReturnsInvalidRequest()
    {
        var req = ValidPhaseRequest();
        var result = RecoveryPlanValidation.ValidatePhase(req, "", null, 30);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidatePhase_StartDayZero_ReturnsInvalidRequest()
    {
        var req = ValidPhaseRequest();
        req.StartDay = 0;
        var result = RecoveryPlanValidation.ValidatePhase(req, "Phase 1", null, 30);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidatePhase_EndDayBeforeStartDay_ReturnsInvalidRequest()
    {
        var req = ValidPhaseRequest();
        req.StartDay = 5;
        req.EndDay = 3;
        var result = RecoveryPlanValidation.ValidatePhase(req, "Phase 1", null, 30);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidatePhase_EndDayExceedsDuration_ReturnsInvalidRequest()
    {
        var req = ValidPhaseRequest();
        req.StartDay = 1;
        req.EndDay = 31;
        var result = RecoveryPlanValidation.ValidatePhase(req, "Phase 1", null, 30);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidatePhase_SleepAndRestExceeds24_ReturnsInvalidRequest()
    {
        var req = ValidPhaseRequest();
        req.SleepAndRestHoursPerDay = 25;
        var result = RecoveryPlanValidation.ValidatePhase(req, "Phase 1", null, 30);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidatePhase_NegativeSortOrder_ReturnsInvalidRequest()
    {
        var req = ValidPhaseRequest();
        req.SortOrder = -1;
        var result = RecoveryPlanValidation.ValidatePhase(req, "Phase 1", null, 30);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    // ──────────────────────────────────────────────
    // ValidateNutrient
    // ──────────────────────────────────────────────

    private static UpsertRecoveryPlanNutrientTargetRequest ValidNutrientRequest() => new()
    {
        NutrientName = "Protein",
        AmountPerDay = 50.00m,
        Unit = "g",
        SortOrder = 0
    };

    [Test]
    public void ValidateNutrient_ValidInputs_ReturnsNone()
    {
        var req = ValidNutrientRequest();
        var result = RecoveryPlanValidation.ValidateNutrient(req, "Protein", "g", null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.None));
    }

    [Test]
    public void ValidateNutrient_AmountZero_ReturnsInvalidRequest()
    {
        var req = ValidNutrientRequest();
        req.AmountPerDay = 0;
        var result = RecoveryPlanValidation.ValidateNutrient(req, "Protein", "g", null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidateNutrient_AmountExceedsMax_ReturnsInvalidRequest()
    {
        var req = ValidNutrientRequest();
        req.AmountPerDay = 100_000_000m;
        var result = RecoveryPlanValidation.ValidateNutrient(req, "Protein", "g", null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidateNutrient_EmptyNutrientName_ReturnsInvalidRequest()
    {
        var req = ValidNutrientRequest();
        var result = RecoveryPlanValidation.ValidateNutrient(req, "", "g", null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    // ──────────────────────────────────────────────
    // ValidateFood
    // ──────────────────────────────────────────────

    private static UpsertRecoveryPlanFoodSourceRequest ValidFoodRequest() => new()
    {
        FoodName = "Chicken Breast",
        SortOrder = 0
    };

    [Test]
    public void ValidateFood_ValidInputs_ReturnsNone()
    {
        var req = ValidFoodRequest();
        var result = RecoveryPlanValidation.ValidateFood(req, "Chicken Breast", null, null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.None));
    }

    [Test]
    public void ValidateFood_EmptyFoodName_ReturnsInvalidRequest()
    {
        var req = ValidFoodRequest();
        var result = RecoveryPlanValidation.ValidateFood(req, "", null, null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidateFood_NegativeSortOrder_ReturnsInvalidRequest()
    {
        var req = ValidFoodRequest();
        req.SortOrder = -1;
        var result = RecoveryPlanValidation.ValidateFood(req, "Chicken", null, null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    [Test]
    public void ValidateFood_SuggestedServingExceedsMax_ReturnsInvalidRequest()
    {
        var req = ValidFoodRequest();
        var longServing = new string('x', RecoveryPlanValidation.MaximumSuggestedServingLength + 1);
        var result = RecoveryPlanValidation.ValidateFood(req, "Chicken", longServing, null);
        Assert.That(result, Is.EqualTo(RecoveryPlanErrorCode.InvalidRequest));
    }

    // ──────────────────────────────────────────────
    // NormalizeOptional
    // ──────────────────────────────────────────────

    [Test]
    public void NormalizeOptional_NullInput_ReturnsNull()
    {
        var result = RecoveryPlanValidation.NormalizeOptional(null);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void NormalizeOptional_WhitespaceOnly_ReturnsNull()
    {
        var result = RecoveryPlanValidation.NormalizeOptional("   ");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void NormalizeOptional_StringWithSpaces_ReturnsTrimmed()
    {
        var result = RecoveryPlanValidation.NormalizeOptional("  hello  ");
        Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void NormalizeOptional_NormalString_ReturnsTrimmed()
    {
        var result = RecoveryPlanValidation.NormalizeOptional("Recovery Plan");
        Assert.That(result, Is.EqualTo("Recovery Plan"));
    }
}
