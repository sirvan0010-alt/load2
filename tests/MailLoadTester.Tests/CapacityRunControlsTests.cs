using Xunit;

namespace MailLoadTester.Tests;

public sealed class CapacityRunControlsTests
{
    [Fact]
    public void Planner_CapsLogicalMessageCountAndEnforcesGlobalRateCeiling()
    {
        var options = CreateOptions(messageCount: 250, intervalMs: 100,
            limits: new CapacityRunLimits(
                Enabled: true,
                MaxMessageCount: 40,
                MaxMessagesPerMinute: 30));

        var planned = CapacityRunPlanner.Apply(options);

        Assert.Equal(40, planned.MessageCount);
        Assert.Equal(2_000, planned.IntervalMs);
        Assert.False(planned.EnableJitter);
    }

    [Fact]
    public void Planner_NeverSpeedsUpAnExistingSlowerInterval()
    {
        var options = CreateOptions(messageCount: 10, intervalMs: 5_000,
            limits: new CapacityRunLimits(
                Enabled: true,
                MaxMessageCount: 10,
                MaxMessagesPerMinute: 60));

        var planned = CapacityRunPlanner.Apply(options);

        Assert.Equal(5_000, planned.IntervalMs);
    }

    [Fact]
    public void Guard_ReservesCumulativeMimeBytesAtomicallyAndNeverOvershootsTarget()
    {
        var guard = new CapacityRunGuard(new CapacityRunLimits(
            Enabled: true,
            MaxMessageCount: 10,
            TargetMimeBytes: 100));

        Assert.True(guard.TryReserve(60, out var firstReason));
        Assert.Equal(string.Empty, firstReason);
        Assert.False(guard.TryReserve(41, out var rejectedReason));
        Assert.Contains("exceed", rejectedReason, StringComparison.OrdinalIgnoreCase);
        Assert.True(guard.TryReserve(40, out _));
        Assert.False(guard.TryReserve(1, out _));

        Assert.Equal(100, guard.ReservedMimeBytes);
        Assert.Equal(2, guard.ReservedAttempts);
    }

    [Fact]
    public void Guard_ConcurrentReservationsCannotOvershootBudget()
    {
        var guard = new CapacityRunGuard(new CapacityRunLimits(
            Enabled: true,
            MaxMessageCount: 100,
            TargetMimeBytes: 100));

        Parallel.For(0, 100, _ => guard.TryReserve(10, out _));

        Assert.Equal(100, guard.ReservedMimeBytes);
        Assert.Equal(10, guard.ReservedAttempts);
    }

    [Fact]
    public void Guard_RejectsSingleMessageAboveConfiguredMimeCeiling()
    {
        var guard = new CapacityRunGuard(new CapacityRunLimits(
            Enabled: true,
            MaxMessageCount: 10,
            MaxMimeMessageBytes: 75));

        Assert.False(guard.TryReserve(76, out var reason));
        Assert.Contains("per-message", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, guard.ReservedMimeBytes);
        Assert.Equal(0, guard.ReservedAttempts);
        Assert.True(guard.TryReserve(75, out _));
    }

    [Fact]
    public void Guard_CountsRetryAttemptsAgainstCumulativeMimeBudget()
    {
        var guard = new CapacityRunGuard(new CapacityRunLimits(
            Enabled: true,
            MaxMessageCount: 10,
            TargetMimeBytes: 150));

        // A retry is another SMTP SEND attempt and consumes another MIME-byte reservation.
        Assert.True(guard.TryReserve(70, out _));
        Assert.True(guard.TryReserve(70, out _));
        Assert.False(guard.TryReserve(20, out _));

        Assert.Equal(140, guard.ReservedMimeBytes);
        Assert.Equal(2, guard.ReservedAttempts);
    }

    [Fact]
    public void Validation_RejectsCapacityRunWithDryRunBecauseNoMimeIsSerialized()
    {
        var options = CreateOptions(messageCount: 10, intervalMs: 0,
            limits: new CapacityRunLimits(Enabled: true), dryRun: true);

        Assert.Throws<ArgumentException>(() => Validation.Validate(options));
    }

    [Fact]
    public void Validation_RejectsOutOfRangeCapacityRate()
    {
        var options = CreateOptions(messageCount: 10, intervalMs: 0,
            limits: new CapacityRunLimits(
                Enabled: true,
                MaxMessageCount: 10,
                MaxMessagesPerMinute: 60_001));

        Assert.Throws<ArgumentException>(() => Validation.Validate(options));
    }

    private static MailTestOptions CreateOptions(
        int messageCount,
        int intervalMs,
        CapacityRunLimits limits,
        bool dryRun = false)
        => new(
            From: "sender@example.test",
            Recipients: new[] { "recipient@example.test" },
            SmtpHost: "smtp.example.test",
            Port: 587,
            Security: SmtpSecurity.StartTls,
            UseAuthentication: false,
            Username: string.Empty,
            Password: string.Empty,
            MessageCount: messageCount,
            IntervalMs: intervalMs,
            BatchMode: false,
            BatchSize: 10,
            BatchPauseSeconds: 1,
            MaxConcurrency: 1,
            Subject: "capacity test",
            Body: "capacity test body",
            DisplayName: "MailLoadTester",
            RandomTestData: false,
            TestMode: true,
            AllowedDomains: "example.test",
            HtmlBody: false,
            Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(),
            IgnoreCertificateErrors: false,
            MaxRetries: 0,
            DryRun: dryRun,
            CapacityRun: limits);
}
