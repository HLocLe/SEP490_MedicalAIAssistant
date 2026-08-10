using MedMateAI.Application.Service;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class ConsultationSessionBuildUserPromptTests
{
    private static DepartmentConsultationQuestion MakeQuestion(
        string text,
        ConsultationQuestionCategory category = ConsultationQuestionCategory.Diagnosis) =>
        new()
        {
            Id = Guid.NewGuid(),
            DepartmentId = Guid.NewGuid(),
            Category = category,
            QuestionText = text,
            SortOrder = 0,
            IsActive = true,
        };

    [Test]
    [Category("B")]
    public void BuildUserPrompt_NoQuestions_ContainsDepartmentSymptomsAndHeader()
    {
        var result = ConsultationSessionService.BuildUserPrompt(
            "Cardiology",
            "chest pain",
            Array.Empty<DepartmentConsultationQuestion>());

        Assert.That(result, Does.Contain("Department: Cardiology"));
        Assert.That(result, Does.Contain("Symptoms: chest pain"));
        Assert.That(result, Does.Contain("Department consultation questions"));
        Assert.That(result, Does.Not.Contain("Chronic diseases:"));
    }

    [Test]
    [Category("B")]
    public void BuildUserPrompt_TrimsDepartmentAndSymptoms()
    {
        var result = ConsultationSessionService.BuildUserPrompt(
            "  Cardiology  ",
            "  chest pain  ",
            Array.Empty<DepartmentConsultationQuestion>());

        Assert.That(result, Does.Contain("Department: Cardiology"));
        Assert.That(result, Does.Contain("Symptoms: chest pain"));
    }

    [Test]
    [Category("N")]
    public void BuildUserPrompt_Questions_AreNumberedWithCategory()
    {
        var questions = new[]
        {
            MakeQuestion("What is the diagnosis?", ConsultationQuestionCategory.Diagnosis),
            MakeQuestion("Which tests do I need?", ConsultationQuestionCategory.Tests),
        };

        var result = ConsultationSessionService.BuildUserPrompt("Internal", "fatigue", questions);

        Assert.That(result, Does.Contain("1. [Diagnosis] What is the diagnosis?"));
        Assert.That(result, Does.Contain("2. [Tests] Which tests do I need?"));
    }

    [Test]
    [Category("B")]
    public void BuildUserPrompt_BlankQuestionText_IsSkipped()
    {
        var questions = new[]
        {
            MakeQuestion("   "),
            MakeQuestion("What treatment options are there?", ConsultationQuestionCategory.Treatment),
        };

        var result = ConsultationSessionService.BuildUserPrompt("General", "fever", questions);

        Assert.That(result, Does.Contain("1. [Treatment] What treatment options are there?"));
        Assert.That(result, Does.Not.Contain("2."));
    }

    [Test]
    [Category("N")]
    public void BuildUserPrompt_MultipleQuestions_AllListedInOrder()
    {
        var questions = new[]
        {
            MakeQuestion("Q1", ConsultationQuestionCategory.Lifestyle),
            MakeQuestion("Q2", ConsultationQuestionCategory.FollowUp),
            MakeQuestion("Q3", ConsultationQuestionCategory.Diagnosis),
        };

        var result = ConsultationSessionService.BuildUserPrompt("General", "checkup", questions);

        Assert.That(result, Does.Contain("1. [Lifestyle] Q1"));
        Assert.That(result, Does.Contain("2. [FollowUp] Q2"));
        Assert.That(result, Does.Contain("3. [Diagnosis] Q3"));
    }
}
