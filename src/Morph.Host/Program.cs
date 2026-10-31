using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Morph.Infrastructure.DependencyInjection;
using Morph.Infrastructure.Hosting;
using Morph.Platform.Api;
using Morph.Plugins.DependencyInjection;
using Morph.Plugins.Hosting;
using Morph.Runtime.Api;
using Serilog;
// Hangfire dashboard extension lives in Infrastructure.

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseMorphSerilog();

    builder.Configuration.AddEnvironmentVariables(prefix: "MORPH_");

    builder.Services.AddMorphInfrastructure(builder.Configuration);
    builder.Services.AddMorphOpenTelemetry();
    builder.Services.AddMorphPlatformApi();
    builder.Services.AddMorphRuntimeApi();
    builder.Services.AddMorphPlugins(builder.Configuration);
    builder.Services.AddHostedService<DatabaseInitializer>();

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

    builder.Services.AddOpenApi();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("MorphDevPanel", policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:5173",
                    "http://127.0.0.1:5173",
                    "http://localhost:5174",
                    "http://127.0.0.1:5174",
                    "http://localhost:4173",
                    "http://127.0.0.1:4173",
                    "http://localhost:4174",
                    "http://127.0.0.1:4174")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseCors("MorphDevPanel");

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<Morph.Infrastructure.Quotas.ProjectQuotaMiddleware>();

    app.UseMorphHangfireDashboard();

    app.MapMorphPlatformApi();
    app.MapMorphRuntimeApi();
    app.MapMorphPlugins();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponseAsync
    });

    app.MapGet("/", () => Results.Ok(new
    {
        service = "Morph.Host",
        phase = 10,
        health = "/health/ready",
        hangfire = "/internal/hangfire",
        graphql = "/graphql",
        sql = "/sql/v1/query",
        data = "/data/v1",
        processes = "/v1/environments/{environmentId}/processes",
        functions = "/v1/environments/{environmentId}/functions",
        sms = "/v1/sms",
        realtime = Morph.Runtime.Api.Realtime.RealtimeChannels.HubPath,
        adminConsole = "http://localhost:5173 (apps/admin-console)"
    }));

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Morph.Host terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";

    var payload = new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.ToString(),
        entries = report.Entries.ToDictionary(
            pair => pair.Key,
            pair => new
            {
                status = pair.Value.Status.ToString(),
                description = pair.Value.Description,
                duration = pair.Value.Duration.ToString()
            })
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}

public partial class Program;
