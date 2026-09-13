using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Xunit;

namespace MailLoadTester.Tests;

public class SmtpOutcomeClassifierTests
{
    static SmtpCommandException Smtp(SmtpStatusCode code, string message) =>
        new(SmtpErrorCode.UnexpectedStatusCode, code, message);

    [Fact]
    public void Success_2xx()
    {
        Assert.Equal(SmtpOutcome.Success, SmtpOutcomeClassifier.ClassifySmtp(250, "OK"));
    }

    [Fact]
    public void Transient_4xx()
    {
        Assert.Equal(SmtpOutcome.Transient, SmtpOutcomeClassifier.Classify(Smtp(SmtpStatusCode.ServiceNotAvailable, "421 temp")));
        Assert.True(SmtpOutcomeClassifier.IsRetryable(Smtp(SmtpStatusCode.ServiceNotAvailable, "421 temp")));
    }

    [Fact]
    public void Throttled_rate_limit_text()
    {
        var o = SmtpOutcomeClassifier.ClassifySmtp(421, "4.7.0 rate limit exceeded");
        Assert.Equal(SmtpOutcome.Throttled, o);
        Assert.True(SmtpOutcomeClassifier.IsRetryable(o));
    }

    [Fact]
    public void Timeout_is_retryable()
    {
        Assert.Equal(SmtpOutcome.Timeout, SmtpOutcomeClassifier.Classify(new TimeoutException()));
        Assert.True(SmtpOutcomeClassifier.IsRetryable(new TimeoutException()));
    }

    [Fact]
    public void Auth_5xx_not_retryable()
    {
        // 535 Authentication credentials invalid (enum name varies by MailKit version)
        var ex = Smtp((SmtpStatusCode)535, "535 auth failed");
        Assert.Equal(SmtpOutcome.Authentication, SmtpOutcomeClassifier.Classify(ex));
        Assert.False(SmtpOutcomeClassifier.IsRetryable(ex));
    }

    [Fact]
    public void Recipient_550_not_retryable()
    {
        var ex = Smtp(SmtpStatusCode.MailboxUnavailable, "550 user unknown");
        Assert.Equal(SmtpOutcome.Recipient, SmtpOutcomeClassifier.Classify(ex));
        Assert.False(SmtpOutcomeClassifier.IsRetryable(ex));
    }

    [Fact]
    public void Permanent_5xx_not_retryable()
    {
        var ex = Smtp(SmtpStatusCode.TransactionFailed, "554 failed");
        var o = SmtpOutcomeClassifier.Classify(ex);
        Assert.True(o is SmtpOutcome.PolicyRejected or SmtpOutcome.Permanent);
        Assert.False(SmtpOutcomeClassifier.IsRetryable(o));
    }

    [Fact]
    public void Cancelled_not_retryable()
    {
        Assert.Equal(SmtpOutcome.Cancelled, SmtpOutcomeClassifier.Classify(new OperationCanceledException()));
        Assert.False(SmtpOutcomeClassifier.IsRetryable(new OperationCanceledException()));
    }

    [Fact]
    public void AuthenticationException()
    {
        Assert.Equal(SmtpOutcome.Authentication, SmtpOutcomeClassifier.Classify(new AuthenticationException("no")));
    }

    [Fact]
    public void Health_category_stable()
    {
        Assert.Equal("timeout", SmtpOutcomeClassifier.ToHealthCategory(SmtpOutcome.Timeout));
        Assert.Equal("auth", SmtpOutcomeClassifier.ToHealthCategory(SmtpOutcome.Authentication));
    }

    [Fact]
    public void Counters_snapshot()
    {
        var c = new SmtpOutcomeCounters();
        c.Record(SmtpOutcome.Success);
        c.Record(SmtpOutcome.Throttled);
        c.Record(SmtpOutcome.Throttled);
        var s = c.Snapshot();
        Assert.Equal(1, s.Success);
        Assert.Equal(2, s.Throttled);
    }

    [Fact]
    public void RetryPolicy_delegates_to_classifier()
    {
        Assert.True(RetryPolicy.IsRetryable(Smtp(SmtpStatusCode.ServiceNotAvailable, "421")));
        Assert.False(RetryPolicy.IsRetryable(Smtp(SmtpStatusCode.MailboxUnavailable, "550")));
    }
}
