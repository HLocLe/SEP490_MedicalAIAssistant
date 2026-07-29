using System.Text;
using AutoMapper;
using MedMateAI.Application.Service;
using MedMateAI.Application.IService;
using MedMateAI.Infrastructure.Identity;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using MedMateAI.Application.Options;
using MedMateAI.Infrastructure.Auth.Options;
using MedMateAI.Infrastructure.Auth.Providers;
using MedMateAI.Infrastructure.Auth.Services;
using MedMateAI.Application.Mapping;
using MedMateAI.Infrastructure.Mapping;
using MedMateAI.Infrastructure.Persistence.Seeder;
using MedMateAI.Infrastructure.Repositories;
using MedMateAI.Infrastructure.Email.Brevo;
using MedMateAI.Infrastructure.Email.Brevo.Options;
using MedMateAI.Infrastructure.AI;
using MedMateAI.Infrastructure.AI.Options;
using MedMateAI.Infrastructure.Payments.PayOS;
using MedMateAI.Infrastructure.NationalInstitutesofHealth;
using MedMateAI.Infrastructure.NationalInstitutesofHealth.Options;
using MedMateAI.Infrastructure.Translation;
using MedMateAI.Infrastructure.Translation.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace MedMateAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException($"Configuration section '{JwtOptions.SectionName}' is missing or invalid.");
        
        services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
        
        //
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        
        //
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMedicalDepartmentService, MedicalDepartmentService>();
        services.AddScoped<IIcdChapterService, IcdChapterService>();
        services.AddScoped<IClinicalQuestionService, ClinicalQuestionService>();
        services.AddScoped<IMedicalFacilityService, MedicalFacilityService>();
        services.AddScoped<IFacilityDepartmentService, FacilityDepartmentService>();
        services.AddScoped<IDoctorService, DoctorService>();
        services.AddScoped<IDoctorInvitationService, DoctorInvitationService>();
        services.AddScoped<IInvitationTokenService, InvitationTokenService>();
        services.AddScoped<IDoctorAccountRegistrationService, DoctorAccountRegistrationService>();
        services.AddScoped<IFeedbackReviewService, FeedbackReviewService>();
        services.AddScoped<IPatientProfileService, PatientProfileService>();
        services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
        services.AddScoped<IAIConfigService, AIConfigService>();
        services.AddScoped<IWebChatbotService, WebChatbotService>();
        services.AddScoped<ISymptomAnalysisService, SymptomAnalysisService>();
        services.AddScoped<IConsultationSessionService, ConsultationSessionService>();
        services.AddScoped<IPayOSService, PayOSService>();
        services.AddScoped<IUserSubscriptionService, UserSubscriptionService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IRecoveryPlanQuotaService, RecoveryPlanQuotaService>();
        services.AddScoped<IRecoveryPlanRequestService, RecoveryPlanRequestService>();
        services.AddScoped<IRecoveryPlanClinicalContextService, RecoveryPlanClinicalContextService>();
        services.AddScoped<IRecoveryPlanService, RecoveryPlanService>();

        //
        services.Configure<PayOSOptions>(configuration.GetSection("PayOS"));
       
        //
        services.Configure<AzureTranslatorOptions>(configuration.GetSection(AzureTranslatorOptions.SectionName));

        services.Configure<BrevoOptions>(configuration.GetSection(BrevoOptions.SectionName));
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        services.AddOptions<RecoveryPlanOptions>()
            .Bind(configuration.GetSection(RecoveryPlanOptions.SectionName))
            .Validate(x => x.AssignmentTimeoutMinutes is > 0 and <= 120,
                "RecoveryPlan AssignmentTimeoutMinutes must be between 1 and 120.")
            .ValidateOnStart();
      
        
        //
        services.Configure<MedGemmaOptions>(configuration.GetSection(MedGemmaOptions.SectionName));
        services.Configure<CloudFlareAIOptions>(configuration.GetSection(CloudFlareAIOptions.SectionName));
        services.Configure<NihClinicalTablesOptions>(configuration.GetSection(NihClinicalTablesOptions.SectionName));
        //
        services.AddHttpContextAccessor();

        //
        services.AddMemoryCache();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration["Redis:ConnectionString"]
                ?? throw new InvalidOperationException("Redis connection string is missing.");
        });

        //
        services.AddHttpClient<BrevoEmailSender>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        //
        services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<BrevoEmailSender>());
        services.AddScoped<IEmailOtpSender>(sp => sp.GetRequiredService<BrevoEmailSender>());
        services.AddHttpClient<IAIChatProvider, OpenRouterChatProvider>();

        //
        services.AddHttpClient<IMedGemmaChatService, MedGemmaChatService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddHttpClient<ICloudFlareAIChatService, CloudFlareAIChatService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        //
        services.AddHttpClient<ITranslationService, AzureTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient<IIcdLookupService, NihIcdLookupService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // 
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        
        //
        services.AddHostedService<IdentitySeedHostedService>();
        
        //
        services.AddOptions<JwtOptions>()
        .Bind(configuration.GetSection(JwtOptions.SectionName))
        .Validate(x =>
           !string.IsNullOrWhiteSpace(x.Secret) &&
            x.Secret.Length >= 32,
           "JWT secret must be at least 32 chars.")
         .ValidateOnStart();
        
        services.AddAutoMapper(cfg => { }, typeof(UserMappingProfile), typeof(PatientProfileMappingProfile), typeof(IcdChapterMappingProfile), typeof(ClinicalQuestionMappingProfile), typeof(MedicalFacilityMappingProfile), typeof(SymptomAnalysisMappingProfile));

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        return services;
    }
}
