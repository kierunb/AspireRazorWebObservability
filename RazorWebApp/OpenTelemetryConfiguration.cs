using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace RazorWebApp;

public static class OpenTelemetryConfiguration
{
    public static void UseOpenTelemetry(
        this WebApplicationBuilder builder,
        bool enableAzureMonitor = false
    )
    {
        var serviceName = builder.Configuration["OpenTelemetry:ServiceName"]!;
        var serviceVersion = builder.Configuration["OpenTelemetry:ServiceVersion"]!;
        var aspireOltpEndpoint = builder.Configuration["OpenTelemetry:AspireOtlpEndpoint"]!;
        var seqOltpEndpoint = builder.Configuration["OpenTelemetry:SeqOtlpEndpoint"]!;

        var resourceAttributes = new Dictionary<string, object>
        {
            { "deployment.environment", builder.Environment.EnvironmentName },
        };


        // logs are stored in the Application Insights - "traces" table
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder
            .Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(serviceName: serviceName, serviceVersion: serviceVersion);
                resource.AddAttributes(resourceAttributes);
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(serviceName);
                metrics.AddMeter("App.Frontend");   // custom metrics
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(serviceName);
                tracing.AddSource("App.Frontend");  // custom activity sources
                tracing.AddHttpClientInstrumentation();
                tracing.AddEntityFrameworkCoreInstrumentation();
                tracing.AddAspNetCoreInstrumentation();
                //tracing.AddSqlClientInstrumentation();
            });

        if (enableAzureMonitor)
        {
            builder
                .Services.AddOpenTelemetry()
                .UseAzureMonitor(config =>
                {
                    config.ConnectionString = builder.Configuration.GetConnectionString(
                        "AppInsights"
                    );
                    config.EnableLiveMetrics = true;
                    // Set the sampling ratio to 10%. This means that 10% of all traces will be sampled and sent to Azure Monitor.
                    //config.SamplingRatio = 0.1F;
                });
        }
        else
        {
            // jaeger, aspire dashboard standalone, otel collector etc.
            builder
                .Services.AddOpenTelemetry()
                .UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(aspireOltpEndpoint)); // Aspire dashboard
        }
    }
}
