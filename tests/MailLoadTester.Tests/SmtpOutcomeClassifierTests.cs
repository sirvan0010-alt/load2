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

    [Theory]
    [InlineData(530, "5.7.0 authentication required")]
    [InlineData(535, "5.7.8 authentication credentials invalid")]
    public void AuthenticationRequired_EnhancedStatus_IsNotRetryable(int code, string response)
    {
        var outcome = SmtpOutcomeClassifier.ClassifySmtp(code, response);
        Assert.Equal(SmtpOutcome.Authentication, outcome);
        Assert.False(SmtpOutcomeClassifier.IsRetryable(outcome));
    }

    [Fact]
    public void AuthenticationRequired_InWrappedOrGenericException_IsClassified()
    {
        var ex = new InvalidOperationException("5.7.0 authentication required");
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

    [Theory]
    [InlineData(552, "552 5.2.2 mailbox full")]
    [InlineData(452, "452 4.2.2 mailbox full")]
    public void MailboxFull_IsDistinctAndNeverRetried(int code, string response)
    {
        var outcome = SmtpOutcomeClassifier.ClassifySmtp(code, response);
        if (code >= 500)
        {
            Assert.Equal(SmtpOutcome.MailboxFull, outcome);
            Assert.False(SmtpOutcomeClassifier.IsRetryable(outcome));
        }
        else
        {
            Assert.Equal(SmtpOutcome.Transient, outcome);
            Assert.True(SmtpOutcomeClassifier.IsRetryable(outcome));
        }
    }

    [Fact]
    public void MessageTooLarge_IsNotRetried()
    {
        var outcome = SmtpOutcomeClassifier.ClassifySmtp(552, "552 5.3.4 message size exceeds fixed maximum message size");
        Assert.Equal(SmtpOutcome.MessageTooLarge, outcome);
        Assert.False(SmtpOutcomeClassifier.IsRetryable(outcome));
    }

    [Fact]
    public void EnhancedPolicyStatus_IsStopAndReview()
    {
        var outcome = SmtpOutcomeClassifier.ClassifySmtp(550, "550 5.7.1 message rejected by policy");
        Assert.Equal(SmtpOutcome.PolicyRejected, outcome);
        Assert.False(SmtpOutcomeClassifier.IsRetryable(outcome));
    }

    [Fact]
    public void MailboxFullAndMessageTooLarge_AreCountedSeparately()
    {
        var counters = new SmtpOutcomeCounters();
        counters.Record(SmtpOutcome.MailboxFull);
        counters.Record(SmtpOutcome.MessageTooLarge);
        var snapshot = counters.Snapshot();
        Assert.Equal(1, snapshot.MailboxFull);
        Assert.Equal(1, snapshot.MessageTooLarge);
    }

    [Fact]
    public void RetryPolicy_delegates_to_classifier()
    {
        Assert.True(RetryPolicy.IsRetryable(Smtp(SmtpStatusCode.ServiceNotAvailable, "421")));
        Assert.False(RetryPolicy.IsRetryable(Smtp(SmtpStatusCode.MailboxUnavailable, "550")));
    }
}
