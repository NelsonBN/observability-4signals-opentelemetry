﻿using BuildingBlocks.Observability;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Api.Users.Infrastructure.Observability;

public static class Setup
{
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services
            .AddOpenTelemetry()
            .UseOtlpExporter()
            .ConfigureResource(TelemetryFactory.Configure)
            .WithTracing(options
                => options
                    .AddSource(Telemetry.Source.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")
                    .AddRedisInstrumentation())
            .WithMetrics(options
                => options
                    .AddMeter(Telemetry.Meter.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddView(
                        "http.server.request.duration",
                        new ExplicitBucketHistogramConfiguration { Boundaries = [0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10] }))
            .WithLogging(options =>
                options.SetResourceBuilder(TelemetryFactory.CreateResource()),
                configureOptions =>
                {
                    configureOptions.IncludeFormattedMessage = true;
                    configureOptions.IncludeScopes = true;
                    configureOptions.ParseStateValues = true;
                });

        services._addHealth();

        return services;
    }

    private static IServiceCollection _addHealth(this IServiceCollection services)
    {
        services
            .AddSingleton<StartupBackgroundService.HealthCheck>()
            .AddHostedService<StartupBackgroundService>()
            .AddHealthChecks()
            .AddMongoDb(
                mongodbConnectionStringFactory: sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("MongoDB")!,
                name: "MongoDB",
                failureStatus: HealthStatus.Unhealthy)
            .AddRedis(
                name: "Redis",
                connectionStringFactory: (sp) => sp.GetRequiredService<IConfiguration>().GetConnectionString("Redis")!,
                failureStatus: HealthStatus.Unhealthy)
            .AddCheck<StartupBackgroundService.HealthCheck>(
                "Startup",
                tags: ["Startup"]);

        return services;
    }

    public static IApplicationBuilder AddObservability(this IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints => endpoints.MapHealthChecks("/healthz/startup", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("Startup"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }));

        app.UseEndpoints(endpoints => endpoints.MapHealthChecks("/healthz/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }));

        app.UseEndpoints(endpoints => endpoints.MapHealthChecks("/healthz/ready", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }));

        return app;
    }
}
