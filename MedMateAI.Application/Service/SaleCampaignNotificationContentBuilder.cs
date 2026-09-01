using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Notifications;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Service;

public sealed class SaleCampaignNotificationContentBuilder :
    ISaleCampaignNotificationContentBuilder
{
    private const int MaximumTitleLength = 100;
    private const int MaximumBodyLength = 220;
    private const int MaximumShortNameLength = 40;
    private const int MaximumCampaignNameLength = 70;

    public SaleCampaignNotificationContent Build(
        SaleCampaignAnnouncementContext context,
        string channel)
    {
        ArgumentNullException.ThrowIfNull(context);
        var shortName = GetShortName(context.DisplayName);
        var campaignName = Truncate(
            Normalize(context.CampaignName) ?? "Ưu đãi MediMate AI",
            MaximumCampaignNameLength);
        var variant = GetVariantIndex(context, channel, 3);

        var title = AudienceTitles(context.EligibilityType, shortName)[variant];
        var body = BenefitBodies(context.BenefitType, campaignName)[variant];
        return new SaleCampaignNotificationContent(
            Truncate(title, MaximumTitleLength),
            Truncate(body, MaximumBodyLength));
    }

    private static IReadOnlyList<string> AudienceTitles(
        SaleCampaignEligibilityType eligibilityType,
        string shortName)
    {
        return eligibilityType switch
        {
            SaleCampaignEligibilityType.FirstPurchase => new[]
            {
                $"{shortName} ơi, bắt đầu cùng MediMate AI tiết kiệm hơn nè 🌱",
                "Lần đầu có deal xinh dành cho bạn 🎁",
                "Một ưu đãi nhỏ cho lần đầu đồng hành 💙"
            },
            SaleCampaignEligibilityType.ReturningCustomer => new[]
            {
                $"{shortName} ơi, MediMate AI có quà cho lần quay lại nè 💙",
                "Chào mừng bạn quay lại 🎉",
                "Có deal mới dành cho bạn nè ✨"
            },
            _ => new[]
            {
                $"{shortName} ơi, có deal mới nè 🎉",
                "Một món quà nhỏ từ MediMate AI 💙",
                "Deal xinh tới rồi ✨"
            }
        };
    }

    private static IReadOnlyList<string> BenefitBodies(
        SaleCampaignBenefitType benefitType,
        string campaignName)
    {
        return benefitType switch
        {
            SaleCampaignBenefitType.PriceOnly => new[]
            {
                $"{campaignName} đang có giá ưu đãi. Mở app để xem ngay nhé!",
                $"Deal giá tốt từ {campaignName} đang chờ bạn khám phá.",
                $"Giá ưu đãi của {campaignName} đã sẵn sàng trên các gói phù hợp."
            },
            SaleCampaignBenefitType.BonusOnly => new[]
            {
                $"{campaignName} đang tặng thêm lượt sử dụng trên các gói phù hợp.",
                $"Có thêm lượt tặng từ {campaignName}. Mở app để xem ngay nhé!",
                $"Quà tặng thêm lượt của {campaignName} đang chờ bạn 🎁"
            },
            _ => new[]
            {
                $"Deal kép từ {campaignName}: giá ưu đãi và thêm lượt trên các gói phù hợp.",
                $"{campaignName} mang tới cả giá tốt lẫn lượt tặng. Xem ngay nhé!",
                $"Ưu đãi kép đang tới rồi 🔥 Khám phá giá tốt và lượt tặng phù hợp."
            }
        };
    }

    private static int GetVariantIndex(
        SaleCampaignAnnouncementContext context,
        string channel,
        int variantCount)
    {
        var stableInput = string.Join(
            ':',
            context.CampaignId.ToString("N"),
            context.UserId.ToString("N"),
            "SaleCampaignAnnouncement",
            channel.Trim().ToUpperInvariant());
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stableInput));
        return (int)(BinaryPrimitives.ReadUInt32LittleEndian(hash) % variantCount);
    }

    private static string GetShortName(string? displayName)
    {
        var normalized = Normalize(displayName);
        if (normalized is null || normalized.Contains('@', StringComparison.Ordinal))
        {
            return "Bạn";
        }

        var lastToken = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];
        return Truncate(lastToken, MaximumShortNameLength);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Truncate(string value, int maximumTextElements)
    {
        var indexes = StringInfo.ParseCombiningCharacters(value);
        return indexes.Length <= maximumTextElements
            ? value
            : value[..indexes[maximumTextElements]];
    }
}
