using System.Text.Json;
using System.Text.Json.Serialization;
using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace MedMateAI.Infrastructure.Realtime.RecoveryPlans;

internal static class RecoveryPlanRealtimeDependencyInjection
{
    internal static IServiceCollection AddRecoveryPlanRealtime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var signalR = services
            .AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            });

        var useRedisBackplane = configuration.GetValue<bool>(
            $"{RecoveryPlanRealtimeConstants.ConfigurationSectionName}:"
            + RecoveryPlanRealtimeConstants.UseRedisBackplaneKey);
        if (useRedisBackplane)
        {
            var redisConnectionString = configuration[
                RecoveryPlanRealtimeConstants.RedisConnectionStringKey];
            if (string.IsNullOrWhiteSpace(redisConnectionString))
            {
                throw new InvalidOperationException(
                    "Recovery Plan SignalR Redis backplane is enabled, but Redis:ConnectionString is missing.");
            }

            signalR.AddStackExchangeRedis(
                redisConnectionString,
                options =>
                {
                    options.Configuration.ChannelPrefix =
                        RedisChannel.Literal(
                            RecoveryPlanRealtimeConstants.RedisChannelPrefix);
                });
        }

        services.AddSingleton<
            IRecoveryPlanRealtimeNotifier,
            RecoveryPlanSignalRNotifier>();

        return services;
    }
}
