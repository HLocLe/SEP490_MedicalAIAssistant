using MedMateAI.Infrastructure.Identity;
using MedMateAI.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedMateAI.Infrastructure.Persistence.Seeder;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        // Migrate via direct PostgreSQL (bypass PgBouncer transaction pooling).
        await MigrateDatabaseAsync(sp, cancellationToken);

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[] { "Admin", "Doctor", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var adminEmail = "admin@medmate.local";
        var adminPassword = "Admin@12345";
        var adminDisplayName = "System Admin";

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin is null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                Status = UserStatus.Confirmed,
                EmailConfirmed = true,
                DisplayName = adminDisplayName,
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        var doctorEmail = "doctor@medmate.local";
        var doctorPassword = "Doctor@12345";
        var doctorDisplayName = "Demo Doctor";

        var existingDoctor = await userManager.FindByEmailAsync(doctorEmail);
        if (existingDoctor is null)
        {
            var doctorUser = new ApplicationUser
            {
                UserName = doctorEmail,
                Email = doctorEmail,
                Status = UserStatus.Confirmed,
                EmailConfirmed = true,
                DisplayName = doctorDisplayName,
            };

            var createResult = await userManager.CreateAsync(doctorUser, doctorPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(doctorUser, "Doctor");
            }
        }
    }

    private static async Task MigrateDatabaseAsync(
        IServiceProvider sp,
        CancellationToken cancellationToken)
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var migrationConnectionString = configuration.GetConnectionString("MigrationConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'MigrationConnection' or 'DefaultConnection' not found.");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(migrationConnectionString);

        await using var migrationContext = new ApplicationDbContext(optionsBuilder.Options);
        await migrationContext.Database.MigrateAsync(cancellationToken);
    }
}

