using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Morph.Infrastructure.Jobs;
using Morph.Infrastructure.Options;

namespace Morph.Infrastructure.DependencyInjection;

public static class HangfireServiceCollectionExtensions
{
    public static IServiceCollection AddMorphHangfire(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        var schema = configuration["Morph:Hangfire:SchemaName"]
                     ?? configuration["MORPH_Hangfire__SchemaName"]
                     ?? "hangfire";

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
            {
                options.UseNpgsqlConnection(connectionString);
            }, new PostgreSqlStorageOptions
            {
                SchemaName = schema,
                PrepareSchemaIfNecessary = true
            }));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Max(1, Environment.ProcessorCount / 2);
            options.Queues = ["default", "mail", "maintenance"];
        });

        services.AddScoped<FileGarbageCollectionJob>();
        services.AddScoped<OutboxCleanupJob>();
        services.AddScoped<EmailSendJob>();

        return services;
    }

    public static WebApplication UseMorphHangfireDashboard(this WebApplication app)
    {
        app.UseHangfireDashboard("/internal/hangfire", new DashboardOptions
        {
            Authorization = [new HangfireDashboardAuthFilter()],
            DashboardTitle = "Morph Jobs"
        });

        RecurringJob.AddOrUpdate<EmailSendJob>(
            EmailSendJob.JobId,
            job => job.Execute(),
            "*/1 * * * *");

        RecurringJob.AddOrUpdate<FileGarbageCollectionJob>(
            FileGarbageCollectionJob.JobId,
            job => job.Execute(),
            Cron.Hourly);

        RecurringJob.AddOrUpdate<OutboxCleanupJob>(
            OutboxCleanupJob.JobId,
            job => job.Execute(),
            Cron.Daily);

        return app;
    }
}
