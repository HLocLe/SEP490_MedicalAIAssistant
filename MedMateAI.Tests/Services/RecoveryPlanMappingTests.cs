using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.Models.RecoveryPlans;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Application.Service;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class RecoveryPlanMappingTests
{
    private static RecoveryPlan MakePlan(Guid? requestId = null)
    {
        return new RecoveryPlan
        {
            Id = Guid.NewGuid(),
            RecoveryPlanRequestId = requestId,
            PlanName = "Test Plan",
            Summary = "Test Summary",
            DurationDays = 7,
            Status = MedMateAI.Domain.Enums.RecoveryPlanStatus.Draft,
            IsCurrent = false,
            Phases = new List<RecoveryPlanPhase>()
        };
    }

    [Test]
    [Category("N")]
    public void ToSummary_ValidPlan_ReturnsMappedSummary()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var plan = MakePlan(requestId);

        // Act
        var result = RecoveryPlanMapping.ToSummary(plan);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(plan.Id));
        Assert.That(result.RecoveryPlanRequestId, Is.EqualTo(requestId));
        Assert.That(result.PlanName, Is.EqualTo(plan.PlanName));
        Assert.That(result.DurationDays, Is.EqualTo(plan.DurationDays));
        Assert.That(result.Status, Is.EqualTo(plan.Status));
        Assert.That(result.IsCurrent, Is.EqualTo(plan.IsCurrent));
    }

    [Test]
    [Category("A")]
    public void ToSummary_NullRequestId_ThrowsInvalidOperationException()
    {
        // Arrange
        var plan = MakePlan(null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => RecoveryPlanMapping.ToSummary(plan));
    }

    [Test]
    [Category("N")]
    public void ToDetail_ValidPlanWithPhases_ReturnsDetailWithSortedAndFilteredPhases()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var plan = MakePlan(requestId);
        
        var deletedPhase = new RecoveryPlanPhase
        {
            Id = Guid.NewGuid(),
            PhaseName = "Deleted",
            StartDay = 1,
            EndDay = 2,
            IsDeleted = true,
            NutrientTargets = new List<RecoveryPlanNutrientTarget>()
        };

        var phase2 = new RecoveryPlanPhase
        {
            Id = Guid.NewGuid(),
            PhaseName = "Phase 2",
            StartDay = 4,
            EndDay = 7,
            SortOrder = 2,
            NutrientTargets = new List<RecoveryPlanNutrientTarget>()
        };

        var phase1 = new RecoveryPlanPhase
        {
            Id = Guid.NewGuid(),
            PhaseName = "Phase 1",
            StartDay = 1,
            EndDay = 3,
            SortOrder = 1,
            NutrientTargets = new List<RecoveryPlanNutrientTarget>()
        };

        plan.Phases = new List<RecoveryPlanPhase> { deletedPhase, phase2, phase1 };

        // Act
        var result = RecoveryPlanMapping.ToDetail(plan);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Phases, Has.Count.EqualTo(2));
        Assert.That(result.Phases[0].Id, Is.EqualTo(phase1.Id)); // phase1 starts at day 1, so it should be first
        Assert.That(result.Phases[1].Id, Is.EqualTo(phase2.Id));
    }

    [Test]
    [Category("A")]
    public void ToDetail_NullRequestId_ThrowsInvalidOperationException()
    {
        // Arrange
        var plan = MakePlan(null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => RecoveryPlanMapping.ToDetail(plan));
    }

    [Test]
    [Category("N")]
    public void ToDoctorDetail_WithSnapshot_ReturnsFullDetail()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var plan = MakePlan(requestId);
        var doctorId = Guid.NewGuid();
        plan.DoctorId = doctorId;
        plan.RecoveryPlanRequest = new RecoveryPlanRequest
        {
            Id = requestId,
            DiseaseGroup = RecoveryPlanDiseaseGroup.Respiratory
        };

        var snapshot = new RecoveryPlanClinicalSnapshot
        {
            SchemaVersion = 1,
            CapturedAtUtc = DateTime.UtcNow,
            RequestId = requestId,
            DiseaseGroup = RecoveryPlanDiseaseGroup.Respiratory,
            PatientProfile = new RecoveryPlanSnapshotPatientProfile
            {
                HeightCm = 175,
                WeightKg = 70,
                Bmi = 22.86
            },
            ChronicDiseases = new List<RecoveryPlanSnapshotChronicDisease>
            {
                new() { DiseaseName = "Hypertension" }
            },
            PrimaryLabTest = new RecoveryPlanSnapshotPrimaryLabTest
            {
                TestSessionId = Guid.NewGuid(),
                Results = new List<RecoveryPlanSnapshotLabResult>
                {
                    new() { IndicatorId = Guid.NewGuid(), Symbol = "HbA1c", UserValue = 6.5 }
                }
            },
            UserMedications = new List<RecoveryPlanSnapshotMedication>
            {
                new() { MedicineName = "Metformin" }
            },
            TreatmentJourney = new RecoveryPlanSnapshotTreatmentJourney
            {
                Id = Guid.NewGuid(),
                Title = "Journey"
            }
        };

        // Act
        var result = RecoveryPlanMapping.ToDoctorDetail(plan, snapshot);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RequestId, Is.EqualTo(requestId));
        Assert.That(result.DiseaseGroup, Is.EqualTo(RecoveryPlanDiseaseGroup.Respiratory));
        Assert.That(result.DoctorId, Is.EqualTo(doctorId));
        Assert.That(result.ClinicalSnapshot, Is.Not.Null);
        Assert.That(result.ClinicalSnapshot!.DiseaseGroup, Is.EqualTo(RecoveryPlanDiseaseGroup.Respiratory));
        Assert.That(result.ClinicalSnapshot.PatientProfile!.HeightCm, Is.EqualTo(175));
        Assert.That(result.ClinicalSnapshot.ChronicDiseases, Has.Count.EqualTo(1));
        Assert.That(result.ClinicalSnapshot.ChronicDiseases[0].DiseaseName, Is.EqualTo("Hypertension"));
        Assert.That(result.ClinicalSnapshot.PrimaryLabTest!.Results, Has.Count.EqualTo(1));
        Assert.That(result.ClinicalSnapshot.PrimaryLabTest.Results[0].Symbol, Is.EqualTo("HbA1c"));
        Assert.That(result.ClinicalSnapshot.UserMedications, Has.Count.EqualTo(1));
        Assert.That(result.ClinicalSnapshot.UserMedications[0].MedicineName, Is.EqualTo("Metformin"));
        Assert.That(result.ClinicalSnapshot.TreatmentJourney!.Title, Is.EqualTo("Journey"));
    }

    [Test]
    [Category("B")]
    public void ToDoctorDetail_NullSnapshot_ReturnsDetailWithNullSnapshot()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var plan = MakePlan(requestId);
        plan.RecoveryPlanRequest = new RecoveryPlanRequest { Id = requestId, DiseaseGroup = RecoveryPlanDiseaseGroup.Respiratory };

        // Act
        var result = RecoveryPlanMapping.ToDoctorDetail(plan, null);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ClinicalSnapshot, Is.Null);
    }

    [Test]
    [Category("N")]
    public void ToPhase_ValidPhase_ReturnsPhaseWithSortedAndFilteredNutrients()
    {
        // Arrange
        var phase = new RecoveryPlanPhase
        {
            Id = Guid.NewGuid(),
            PhaseName = "Phase 1",
            StartDay = 1,
            EndDay = 5,
            SleepAndRestHoursPerDay = 10m,
            Instruction = "Sleep well",
            SortOrder = 1,
            NutrientTargets = new List<RecoveryPlanNutrientTarget>
            {
                new() { Id = Guid.NewGuid(), NutrientName = "Vitamin C", SortOrder = 2, IsDeleted = false },
                new() { Id = Guid.NewGuid(), NutrientName = "Deleted", SortOrder = 1, IsDeleted = true },
                new() { Id = Guid.NewGuid(), NutrientName = "Protein", SortOrder = 1, IsDeleted = false }
            }
        };

        // Act
        var result = RecoveryPlanMapping.ToPhase(phase);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PhaseName, Is.EqualTo(phase.PhaseName));
        Assert.That(result.NutrientTargets, Has.Count.EqualTo(2));
        Assert.That(result.NutrientTargets[0].NutrientName, Is.EqualTo("Protein"));
        Assert.That(result.NutrientTargets[1].NutrientName, Is.EqualTo("Vitamin C"));
    }

    [Test]
    [Category("N")]
    public void ToNutrient_ValidNutrient_ReturnsNutrientWithSortedAndFilteredFoods()
    {
        // Arrange
        var nutrient = new RecoveryPlanNutrientTarget
        {
            Id = Guid.NewGuid(),
            NutrientName = "Protein",
            AmountPerDay = 50m,
            Unit = "g",
            Instruction = "Eat protein",
            SortOrder = 1,
            FoodSources = new List<RecoveryPlanFoodSource>
            {
                new() { Id = Guid.NewGuid(), FoodName = "Fish", SortOrder = 2, IsDeleted = false },
                new() { Id = Guid.NewGuid(), FoodName = "Deleted", SortOrder = 1, IsDeleted = true },
                new() { Id = Guid.NewGuid(), FoodName = "Chicken", SortOrder = 1, IsDeleted = false }
            }
        };

        // Act
        var result = RecoveryPlanMapping.ToNutrient(nutrient);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.NutrientName, Is.EqualTo(nutrient.NutrientName));
        Assert.That(result.FoodSources, Has.Count.EqualTo(2));
        Assert.That(result.FoodSources[0].FoodName, Is.EqualTo("Chicken"));
        Assert.That(result.FoodSources[1].FoodName, Is.EqualTo("Fish"));
    }

    [Test]
    [Category("N")]
    public void ToFood_ValidFood_ReturnsMappedFood()
    {
        // Arrange
        var food = new RecoveryPlanFoodSource
        {
            Id = Guid.NewGuid(),
            FoodName = "Apple",
            SuggestedServing = "1 medium",
            Note = "Eat fresh",
            SortOrder = 3
        };

        // Act
        var result = RecoveryPlanMapping.ToFood(food);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.FoodName, Is.EqualTo(food.FoodName));
        Assert.That(result.SuggestedServing, Is.EqualTo(food.SuggestedServing));
        Assert.That(result.Note, Is.EqualTo(food.Note));
        Assert.That(result.SortOrder, Is.EqualTo(food.SortOrder));
    }

    [Test]
    [Category("N")]
    public void ToPage_ValidPagedResult_ReturnsPagedResponse()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var items = new List<RecoveryPlan> { MakePlan(requestId) };
        var pagedResult = new PagedResult<RecoveryPlan>
        {
            Items = items,
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPages = 1
        };

        // Act
        var result = RecoveryPlanMapping.ToPage(pagedResult);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PageNumber, Is.EqualTo(1));
        Assert.That(result.PageSize, Is.EqualTo(10));
        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].RecoveryPlanRequestId, Is.EqualTo(requestId));
    }
}
