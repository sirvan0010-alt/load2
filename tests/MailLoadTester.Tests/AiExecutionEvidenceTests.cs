using Xunit;
using MailLoadTester;

namespace MailLoadTester.Tests;

public sealed class AiExecutionEvidenceTests
{
    [Fact]
    public void Evidence_Projects_Existing_Result_Without_Secrets()
    {
        var action = new AiAction(
            "a1", AiActionKind.LoadTest, "LOAD_ENGINE_AGENT",
            new[] { "smtp.example.test" }, 10, 2, 0);

        var result = CreateResult();
        var evidence = AiExecutionVerifier.FromResult(
            action, result,
            DateTimeOffset.UtcNow.AddSeconds(-1),
            DateTimeOffset.UtcNow);

        Assert.Equal("completed-with-failures", evidence.Status);
        Assert.Equal(result.Sent, evidence.Sent);
        Assert.Equal(result.Failed, evidence.Failed);
        Assert.Equal(result.Smtp4xx, evidence.Smtp4xx);
    }

    [Fact]
    public void Verification_Fails_When_Result_Exceeds_Action_Budget()
    {
        var action = new AiAction(
            "a1", AiActionKind.LoadTest, "LOAD_ENGINE_AGENT",
            new[] { "smtp.example.test" }, 5, 1, 0);

        var result = CreateResult(requested: 5, sent: 6, failed: 0);
        var verification = AiExecutionVerifier.Verify(action, result);

        Assert.False(verification.Passed);
        Assert.Contains(verification.Findings,
            x => x.Contains("sent count", StringComparison.OrdinalIgnoreCase));
    }

    private static MailTestResult CreateResult(
        int requested = 10,
        int sent = 7,
        int failed = 2) =>
        new()
        {
            Requested = requested,
            Sent = sent,
            Failed = failed,
            Smtp4xx = 1,
            Smtp5xx = 0,
            Timeouts = 0,
            Retries = 1,
            ThroughputPerSec = 2.5,
            P95LatencyMs = 120,
            CircuitBreakerOpen = false,
            Cancelled = false
        };
}
