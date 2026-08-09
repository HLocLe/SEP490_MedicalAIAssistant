using Hangfire;
using Hangfire.PostgreSql;
using MedMateAI.Application.IService;
using MedMateAI.Application.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedMateAI.Infrastructure.BackgroundJobs;

public static class HangfireDependencyInjection
{
    public static IServiceCollection AddHangfireBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
       
        var connectionString = configuration.GetConnectionString("HangfireConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string for Hangfire not found.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 10;
        });

        services.AddScoped<ILabTestOcrProcessor, LabTestOcrProcessor>();
        services.AddScoped<LabTestOcrJob>();
        services.AddSingleton<ILabTestJobScheduler, HangfireLabTestJobScheduler>();

        services.AddScoped<ConsultationDoctorQuestionsJob>();
        services.AddSingleton<IConsultationSessionJobScheduler, HangfireConsultationSessionJobScheduler>();

        return services;
    }
}
