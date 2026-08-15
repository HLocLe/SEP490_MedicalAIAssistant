using MedMateAI.Application.DTOs.AIConfigs.Responses;
using MedMateAI.Application.DTOs.WebChatbot.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Service;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class LabTestOcrStructurerTests
{
    private Mock<IAIConfigService> _aiConfigServiceMock = null!;
    private Mock<IAIChatProvider> _chatProviderMock = null!;
    private Mock<ILogger<LabTestOcrStructurer>> _loggerMock = null!;
    private LabTestOcrStructurer _structurer = null!;

    [SetUp]
    public void SetUp()
    {
        _aiConfigServiceMock = new Mock<IAIConfigService>();
        _chatProviderMock = new Mock<IAIChatProvider>();
        _loggerMock = new Mock<ILogger<LabTestOcrStructurer>>();

        _structurer = new LabTestOcrStructurer(
            _aiConfigServiceMock.Object,
            _chatProviderMock.Object,
            _loggerMock.Object);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task StructureAsync_BlankRawOcrText_ReturnsEmptyWithoutCallingAI(string? rawOcrText)
    {
        var result = await _structurer.StructureAsync(rawOcrText!, CancellationToken.None);

        Assert.That(result, Is.Empty);
        _aiConfigServiceMock.Verify(
            service => service.GetActiveAIConfigByTaskTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void StructureAsync_NoActiveAIConfig_ThrowsInvalidOperationException()
    {
        _aiConfigServiceMock.Setup(service => service.GetActiveAIConfigByTaskTypeAsync("LabTestOcrStructuring", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIConfigResponse?)null);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _structurer.StructureAsync("raw text", CancellationToken.None));
    }

    [Test]
    public void StructureAsync_AIConfigMissingSystemPrompt_ThrowsInvalidOperationException()
    {
        _aiConfigServiceMock.Setup(service => service.GetActiveAIConfigByTaskTypeAsync("LabTestOcrStructuring", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIConfigResponse { SystemPrompt = "  " });

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _structurer.StructureAsync("raw text", CancellationToken.None));
    }

    [Test]
    public void StructureAsync_ChatResponseHasNoJsonObject_ThrowsInvalidOperationException()
    {
        SetupAIConfig();
        SetupChatResult("no json here at all");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _structurer.StructureAsync("raw text", CancellationToken.None));
    }

    [Test]
    public void StructureAsync_ChatResponseHasInvalidJson_ThrowsInvalidOperationException()
    {
        SetupAIConfig();
        SetupChatResult("```json\n{ this is not valid json }\n```");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _structurer.StructureAsync("raw text", CancellationToken.None));
    }

    [Test]
    public async Task StructureAsync_EmptyRowsInDocument_ReturnsEmpty()
    {
        SetupAIConfig();
        SetupChatResult("{ \"danh_sach_xet_nghiem\": [] }");

        var result = await _structurer.StructureAsync("raw text", CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task StructureAsync_ValidDocument_ParsesRowsAndSkipsBlankTestNames()
    {
        SetupAIConfig();
        SetupChatResult("""
            Here is the result:
            ```json
            {
              "danh_sach_xet_nghiem": [
                { "ten_xet_nghiem": "Glucose", "ket_qua": "5,1", "tri_so_binh_thuong": " 3.9-6.4 " },
                { "ten_xet_nghiem": "  ", "ket_qua": "1.0", "tri_so_binh_thuong": "1-2" },
                { "ten_xet_nghiem": "Cholesterol", "ket_qua": "not-a-number", "tri_so_binh_thuong": null }
              ]
            }
            ```
            """);

        var result = await _structurer.StructureAsync("raw text", CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].TestName, Is.EqualTo("Glucose"));
            Assert.That(result[0].Value, Is.EqualTo(5.1));
            Assert.That(result[0].ReferenceText, Is.EqualTo("3.9-6.4"));
            Assert.That(result[1].TestName, Is.EqualTo("Cholesterol"));
            Assert.That(result[1].Value, Is.Null);
            Assert.That(result[1].ReferenceText, Is.Null);
        });
    }

    [Test]
    public async Task StructureAsync_PassesAIConfigValuesToChatProvider()
    {
        _aiConfigServiceMock.Setup(service => service.GetActiveAIConfigByTaskTypeAsync("LabTestOcrStructuring", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIConfigResponse
            {
                SystemPrompt = "system prompt",
                Model = "gpt-test",
                Temperature = 0.2m,
                MaxTokens = 512
            });
        SetupChatResult("{ \"danh_sach_xet_nghiem\": [] }");

        await _structurer.StructureAsync("raw text", CancellationToken.None);

        _chatProviderMock.Verify(provider => provider.GenerateAsync(
            It.Is<MedMateAI.Application.DTOs.WebChatbot.Requests.AIProviderChatRequest>(request =>
                request.SystemPrompt == "system prompt"
                && request.UserMessage == "raw text"
                && request.Model == "gpt-test"
                && request.Temperature == 0.2m
                && request.MaxTokens == 512),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupAIConfig()
    {
        _aiConfigServiceMock.Setup(service => service.GetActiveAIConfigByTaskTypeAsync("LabTestOcrStructuring", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIConfigResponse { SystemPrompt = "system prompt" });
    }

    private void SetupChatResult(string content)
    {
        _chatProviderMock.Setup(provider => provider.GenerateAsync(
                It.IsAny<MedMateAI.Application.DTOs.WebChatbot.Requests.AIProviderChatRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProviderChatResult { Content = content });
    }
}
