using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DesktopAiTestAgent.AgentRunner;

/// <summary>
/// OpenTelemetry instrumentation for the agent loop (OBS-1).
///
/// Tracing/metrics are emitted through a single <see cref="ActivitySource"/> and
/// <see cref="Meter"/> named <c>DesktopAiTestAgent.Runner</c>. Creating activities is
/// near-free when nobody is listening: <see cref="ActivitySource.StartActivity(string)"/>
/// returns <c>null</c> unless an exporter (or test listener) has subscribed, so the
/// runner stays manual-first / AI-optional — no collector, no cost, no failure.
///
/// Export is strictly opt-in via <see cref="TryStartExport"/>: only when an OTLP
/// endpoint is configured does a provider get built. On .NET Framework the OTLP
/// exporter must use HTTP/protobuf (gRPC was dropped in exporter 1.12.0), so we force
/// <see cref="OtlpExportProtocol.HttpProtobuf"/> regardless of target.
/// </summary>
public static class RunnerTelemetry
{
    public const string SourceName = "DesktopAiTestAgent.Runner";
    public const string Version = "1.0.0";

    /// <summary>Standard OTLP endpoint env var (e.g. <c>http://localhost:4318</c>).</summary>
    public const string EndpointEnvVar = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static readonly ActivitySource Source = new(SourceName, Version);
    public static readonly Meter Meter = new(SourceName, Version);

    public static readonly Histogram<double> StepDuration =
        Meter.CreateHistogram<double>("agentloop.step.duration", unit: "ms", description: "Duration of one agent step.");
    public static readonly Histogram<double> RunDuration =
        Meter.CreateHistogram<double>("agentloop.run.duration", unit: "ms", description: "Duration of a full agent run.");
    public static readonly Counter<long> StepCount =
        Meter.CreateCounter<long>("agentloop.step.count", unit: "{step}", description: "Agent steps, tagged by outcome.");
    public static readonly Histogram<long> RunScore =
        Meter.CreateHistogram<long>("agentloop.run.score", unit: "{score}", description: "Final score of a run.");

    /// <summary>
    /// Builds OTLP trace + metric providers when an endpoint is configured, otherwise
    /// returns <c>null</c> (export disabled — the common, dependency-free path). The
    /// returned handle must be disposed at process end to flush pending telemetry.
    /// </summary>
    public static IDisposable? TryStartExport(WorkflowConfig config)
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointEnvVar);
        if (string.IsNullOrWhiteSpace(endpoint) || !Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
            return null;

        void ConfigureOtlp(OtlpExporterOptions o)
        {
            o.Endpoint = endpointUri;
            o.Protocol = OtlpExportProtocol.HttpProtobuf; // net48-safe; gRPC unsupported there
        }

        var resource = ResourceBuilder.CreateDefault().AddService("desktop-ai-test-agent");

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .SetResourceBuilder(resource)
            .AddOtlpExporter(ConfigureOtlp)
            .Build();

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(SourceName)
            .SetResourceBuilder(resource)
            .AddOtlpExporter(ConfigureOtlp)
            .Build();

        return new ExportHandle(tracerProvider, meterProvider);
    }

    private sealed class ExportHandle(IDisposable tracerProvider, IDisposable meterProvider) : IDisposable
    {
        public void Dispose()
        {
            // Dispose flushes pending spans/metrics through the OTLP exporter.
            tracerProvider.Dispose();
            meterProvider.Dispose();
        }
    }
}
