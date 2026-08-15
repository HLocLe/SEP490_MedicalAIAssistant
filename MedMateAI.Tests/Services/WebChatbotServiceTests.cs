using MedMateAI.Application.DTOs.AIConfigs.Responses;
using MedMateAI.Application.DTOs.SubscriptionPlans.Responses;
using MedMateAI.Application.DTOs.WebChatbot.Requests;
using MedMateAI.Application.DTOs.WebChatbot.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Service;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class WebChatbotServiceTests
{
    private Mock<ISubscriptionPlanService> _subscriptionPlanServiceMock = null!;
    private Mock<IAIConfigService> _aiConfigServiceMock = null!;
    private Mock<IAIChatProvider> _aiChatProviderMock = null!;
    private WebChatbotService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _subscriptionPlanServiceMock = new Mock<ISubscriptionPlanService>();
        _aiConfigServiceMock = new Mock<IAIConfigService>();
        _aiChatProviderMock = new Mock<IAIChatProvider>();

        _subscriptionPlanServiceMock.Setup(service => service.ListActiveSubscriptionPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SubscriptionPlanResponse>());

        _service = new WebChatbotService(
            _subscriptionPlanServiceMock.Object,
            _aiConfigServiceMock.Object,
            _aiChatProviderMock.Object);
    }

    [Test]
    public void SendMessageAsync_NullRequest_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.SendMessageAsync(null!, CancellationToken.None));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void SendMessageAsync_BlankMessage_ThrowsArgumentException(string? message)
    {
        var request = new WebChatbotRequest { Message = message! };

        Assert.ThrowsAsync<ArgumentException>(() => _service.SendMessageAsync(request, CancellationToken.None));
    }

    [Test]
    public void SendMessageAsync_MessageTooLong_ThrowsArgumentException()
    {
        var request = new WebChatbotRequest { Message = new string('a', 2001) };

        Assert.ThrowsAsync<ArgumentException>(() => _service.SendMessageAsync(request, CancellationToken.None));
    }

    [Test]
    public async Task SendMessageAsync_NoPrimaryConfig_FallsBackToLegacyTaskType()
    {
        _aiConfigServiceMock.Setup(service => service.GetActiveAIConfigByTaskTypeAsync("WebFrontDeskAssistant", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIConfigResponse?)null);
        _aiConfigServiceMock.Setup(service => service.GetActiveAIConfigByTaskTypeAsync("WebSubscriptionAdvisor", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIConfigResponse { SystemPrompt = "legacy prompt", Model = "legacy-model" });
        SetupChatResult("""{"answer":"hi","intent":"Greeting","needsMoreInformation":false,"recommendedPlanIds":[]}""");

        await _service.SendMessageAsync(new WebChatbotRequest { Message = "hello" }, CancellationToken.None);

        _aiConfigServiceMock.Verify(service => service.GetActiveAIConfigByTaskTypeAsync("WebFrontDeskAssistant", It.IsAny<CancellationToken>()), Times.Once);
        _aiConfigServiceMock.Verify(service => service.GetActiveAIConfigByTaskTypeAsync("WebSubscriptionAdvisor", It.IsAny<CancellationToken>()), Times.Once);
        _aiChatProviderMock.Verify(provider => provider.GenerateAsync(
            It.Is<AIProviderChatRequest>(r => r.SystemPrompt == "legacy prompt" && r.Model == "legacy-model"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SendMessageAsync_NoActiveConfig_UsesFallbackPromptAndDefaults()
    {
        SetupNoAIConfig();
        SetupChatResult("""{"answer":"hi","intent":"Greeting","needsMoreInformation":false,"recommendedPlanIds":[]}""");

        await _service.SendMessageAsync(new WebChatbotRequest { Message = "hello" }, CancellationToken.None);

        _aiChatProviderMock.Verify(provider => provider.GenerateAsync(
            It.Is<AIProviderChatRequest>(r =>
                r.SystemPrompt.Contains("MedMateAI")
                && r.Model == string.Empty
                && r.Temperature == 0.3m
                && r.MaxTokens == 800),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SendMessageAsync_ValidResponse_FiltersToActivePlansAndDedupes()
    {
        var activePlanId = Guid.NewGuid();
        var inactivePlanId = Guid.NewGuid();
        var activePlans = new[]
        {
            new SubscriptionPlanResponse { Id = activePlanId, PlanName = "Basic", Price = 100, DurationInDays = 30 }
        };
        _subscriptionPlanServiceMock.Setup(service => service.ListActiveSubscriptionPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activePlans);
        SetupNoAIConfig();
        SetupChatResult($$"""
            {"answer":"Goi y goi Basic","intent":"SubscriptionRecommendation","needsMoreInformation":false,
             "recommendedPlanIds":["{{activePlanId}}","{{activePlanId}}","{{inactivePlanId}}"]}
            """);

        var result = await _service.SendMessageAsync(new WebChatbotRequest { Message = "toi can goi nao" }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Answer, Is.EqualTo("Goi y goi Basic"));
            Assert.That(result.Intent, Is.EqualTo("SubscriptionRecommendation"));
            Assert.That(result.RecommendedPlans, Has.Count.EqualTo(1));
            Assert.That(result.RecommendedPlans[0].Id, Is.EqualTo(activePlanId));
        });
    }

    [Test]
    public async Task SendMessageAsync_MarkdownWrappedJsonResponse_ParsesSuccessfully()
    {
        SetupNoAIConfig();
        SetupChatResult("""
            ```json
            {"answer":"chao ban","intent":"Greeting","needsMoreInformation":false,"recommendedPlanIds":[]}
            ```
            """);

        var result = await _service.SendMessageAsync(new WebChatbotRequest { Message = "chao" }, CancellationToken.None);

        Assert.That(result.Answer, Is.EqualTo("chao ban"));
    }

    [TestCase("")]
    [TestCase("not json at all")]
    public async Task SendMessageAsync_UnparsableAIResponse_ReturnsSafeFallback(string content)
    {
        SetupNoAIConfig();
        SetupChatResult(content);

        var result = await _service.SendMessageAsync(new WebChatbotRequest { Message = "hello" }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Answer, Does.Contain("Xin loi"));
            Assert.That(result.RecommendedPlans, Is.Empty);
            Assert.That(result.Intent, Is.EqualTo("Unknown"));
            Assert.That(result.NeedsMoreInformation, Is.True);
        });
    }

    [Test]
    public async Task SendMessageAsync_EmptyAnswerFromAI_UsesFallbackEmptyAnswerText()
    {
        SetupNoAIConfig();
        SetupChatResult("""{"answer":"   ","intent":"Unknown","needsMoreInformation":false,"recommendedPlanIds":[]}""");

        var result = await _service.SendMessageAsync(new WebChatbotRequest { Message = "hello" }, CancellationToken.None);

        Assert.That(result.Answer, Does.Contain("da nhan duoc"));
    }

    [Test]
    public async Task SendMessageAsync_UnsupportedIntent_NormalizesToUnknown()
    {
        SetupNoAIConfig();
        SetupChatResult("""{"answer":"hi","intent":"SomethingElse","needsMoreInformation":false,"recommendedPlanIds":[]}""");

        var result = await _service.SendMessageAsync(new WebChatbotRequest { Message = "hello" }, CancellationToken.None);

        Assert.That(result.Intent, Is.EqualTo("Unknown"));
    }

    private void SetupNoAIConfig()
    {
        _aiConfigServiceMock.Setup(service => service.GetActiveAIConfigByTaskTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIConfigResponse?)null);
    }

    private void SetupChatResult(string content)
    {
        _aiChatProviderMock.Setup(provider => provider.GenerateAsync(It.IsAny<AIProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProviderChatResult { Content = content });
    }
}
