using System.Text.Json;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class DashboardStateTests
{
    [Fact]
    public void State_serializes_live_queue_and_worker_metrics()
    {
        var state = new DashboardState(
            Sent: 12,
            Failed: 1,
            Requested: 30,
            ProgressPct: 43.3,
            ThroughputPerSec: 4.5,
            EtaSeconds: 4,
            Phase: "Sending",
            LogLine: "SMTP SEND",
            QueueDepth: 6,
            QueuePeakDepth: 10,
            WorkerLimit: 8);

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.Contains("\"queueDepth\":6", json, StringComparison.Ordinal);
        Assert.Contains("\"queuePeakDepth\":10", json, StringComparison.Ordinal);
        Assert.Contains("\"workerLimit\":8", json, StringComparison.Ordinal);
    }
}
