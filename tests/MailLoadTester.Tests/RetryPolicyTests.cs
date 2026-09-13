using MailKit;
using MailKit.Net.Smtp;
using Xunit;

namespace MailLoadTester.Tests;

public class RetryPolicyTests
{
    static SmtpCommandException Smtp(SmtpStatusCode code, string message) =>
        new(SmtpErrorCode.UnexpectedStatusCode, code, message);

    [Fact]
    public void IsRetryable_4xx_true()
    {
        Assert.True(RetryPolicy.IsRetryable(Smtp(SmtpStatusCode.ServiceNotAvailable, "421 temp")));
    }

    [Fact]
    public void IsRetryable_5xx_false()
    {
        Assert.False(RetryPolicy.IsRetryable(Smtp(SmtpStatusCode.MailboxUnavailable, "550 no")));
    }

    [Fact]
    public void IsRetryable_Timeout_true()
    {
        Assert.True(RetryPolicy.IsRetryable(new TimeoutException("t")));
    }

    [Fact]
    public void IsRetryable_Cancel_false()
    {
        Assert.False(RetryPolicy.IsRetryable(new OperationCanceledException()));
    }

    [Fact]
    public void GetDelay_positive_and_capped()
    {
        var d0 = RetryPolicy.GetDelay(0);
        var d5 = RetryPolicy.GetDelay(5);
        Assert.True(d0.TotalMilliseconds >= 100);
        Assert.True(d5.TotalMilliseconds <= 10_000);
    }

    [Fact]
    public void GlobalBudget_scales_with_messages()
    {
        Assert.Equal(0, RetryPolicy.GlobalBudget(0, 100));
        Assert.Equal(30, RetryPolicy.GlobalBudget(3, 10));
    }
}

public class RetryMetricsTests
{
    [Fact]
    public void Snapshot_tracks_attempts_and_outcomes()
    {
        var m = new RetryMetrics();
        m.RecordRetryableFailure();
        m.RecordRetryAttempt(1);
        m.RecordRetryAttempt(2);
        m.RecordSuccessAfterRetry();
        m.RecordExhausted();
        m.RecordPermanentFailure();
        m.RecordBudgetExhausted();

        var s = m.Snapshot();
        Assert.Equal(1, s.RetryableFailures);
        Assert.Equal(2, s.RetryAttempts);
        Assert.Equal(1, s.SuccessAfterRetry);
        Assert.Equal(1, s.Exhausted);
        Assert.Equal(1, s.PermanentFailures);
        Assert.Equal(1, s.BudgetExhausted);
        Assert.Equal(1, s.AttemptsByNumber[1]);
        Assert.Equal(1, s.AttemptsByNumber[2]);
    }

    [Fact]
    public void Concurrent_increments_are_safe()
    {
        var m = new RetryMetrics();
        Parallel.For(0, 200, _ =>
        {
            m.RecordRetryableFailure();
            m.RecordRetryAttempt(1);
        });
        var s = m.Snapshot();
        Assert.Equal(200, s.RetryableFailures);
        Assert.Equal(200, s.RetryAttempts);
    }
}
