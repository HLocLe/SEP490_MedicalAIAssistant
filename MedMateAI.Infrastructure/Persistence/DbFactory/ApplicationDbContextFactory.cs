using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MedMateAI.Infrastructure.Persistence.DbFactory;

public sealed class ApplicationDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var basePath = ResolveAppSettingsBasePath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("MigrationConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'MigrationConnection' or 'DefaultConnection' not found.");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string ResolveAppSettingsBasePath()
    {
        var current = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(current, "appsettings.json")))
        {
            return current;
        }

        var startupProject = Path.GetFullPath(Path.Combine(current, "..", "MedMateAI"));
        if (File.Exists(Path.Combine(startupProject, "appsettings.json")))
        {
            return startupProject;
        }

        throw new InvalidOperationException(
            $"Could not find appsettings.json from '{current}' or '{startupProject}'.");
    }
}

