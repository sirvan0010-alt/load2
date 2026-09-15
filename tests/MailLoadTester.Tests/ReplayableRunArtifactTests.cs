using System.Text.Json.Nodes;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class ReplayableRunArtifactTests
{
    private sealed record Scenario(string Kind, string Target, string ApiToken);
    private sealed record Configuration(string Host, string Username, string Password, int MaxConcurrency);
    private sealed record Event(string Recipient, string Outcome, string ClientSecret);
    private sealed record Result(int Sent, int Failed, string Error);

    [Fact]
    public void Create_IsDeterministicAndRedactsSecrets()
    {
        var scenario = new Scenario("MailboxQuota", "lab.test", "scenario-token");
        var configuration = new Configuration("smtp.lab", "user", "smtp-password", 2);
        var events = new object[] { new Event("a@lab.test", "Accepted", "event-secret") };
        var result = new Result(1, 0, "");

        var a = ReplayableRunArtifactBuilder.Create(
            "run-fixed", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            scenario, configuration, events, result);
        var b = ReplayableRunArtifactBuilder.Create(
            "run-fixed", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            scenario, configuration, events, result);

        Assert.Equal(a.Sha256, b.Sha256);
        var json = ReplayableRunArtifactBuilder.ToJson(a);
        Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
        Assert.DoesNotContain("scenario-token", json, StringComparison.Ordinal);
        Assert.DoesNotContain("smtp-password", json, StringComparison.Ordinal);
        Assert.DoesNotContain("event-secret", json, StringComparison.Ordinal);
        Assert.Contains("run-fixed", json, StringComparison.Ordinal);
        Assert.Contains("MailboxQuota", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_RedactsNestedSecretKeys()
    {
        var scenario = new JsonObject
        {
            ["kind"] = "test",
            ["nested"] = new JsonObject { ["clientCertificatePassword"] = "hidden" }
        };

        var artifact = ReplayableRunArtifactBuilder.Create(
            "run-nested", DateTimeOffset.UtcNow, scenario,
            new { host = "localhost" }, Array.Empty<object>(), new { ok = true });

        Assert.Equal("[REDACTED]", artifact.Scenario["nested"]!["clientCertificatePassword"]!.GetValue<string>());
    }

    [Fact]
    public async Task WriteAsync_UsesSafeRunIdAndPersistsArtifact()
    {
        var root = Path.Combine(Path.GetTempPath(), "load2-replay-" + Guid.NewGuid().ToString("N"));
        try
        {
            var artifact = ReplayableRunArtifactBuilder.Create(
                "run/unsafe:id", DateTimeOffset.UtcNow,
                new { kind = "lab" }, new { host = "localhost" },
                new object[] { new { step = 1 } }, new { sent = 1 });

            var path = await ReplayableRunArtifactBuilder.WriteAsync(artifact, root);
            Assert.True(File.Exists(path));
            Assert.DoesNotContain('/', Path.GetFileName(path));
            Assert.DoesNotContain(':', Path.GetFileName(path));
            Assert.Contains("run_unsafe_id.json", path, StringComparison.Ordinal);
            var persisted = await File.ReadAllTextAsync(path);
            Assert.Contains(artifact.Sha256, persisted, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_HonorsCancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), "load2-replay-cancel-" + Guid.NewGuid().ToString("N"));
        try
        {
            var artifact = ReplayableRunArtifactBuilder.Create(
                "cancel", DateTimeOffset.UtcNow,
                new { kind = "lab" }, new { host = "localhost" }, null, new { sent = 0 });
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                ReplayableRunArtifactBuilder.WriteAsync(artifact, root, cts.Token));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
