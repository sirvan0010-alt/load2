using MailLoadTester.Core;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class SecretRedactorTests
{
    [Fact]
    public void RedactsSensitiveEnvironmentStyleLines()
    {
        var input = "API_TOKEN=super-secret\nPASSWORD=hunter2\nSAFE=value\n";
        var output = SecretRedactor.Redact(input);

        Assert.DoesNotContain("super-secret", output, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", output, StringComparison.Ordinal);
        Assert.Contains("API_TOKEN=[REDACTED]", output, StringComparison.Ordinal);
        Assert.Contains("PASSWORD=[REDACTED]", output, StringComparison.Ordinal);
        Assert.Contains("SAFE=value", output, StringComparison.Ordinal);
    }

    [Fact]
    public void LeavesOrdinaryOutputUntouched()
    {
        const string input = "build=passed\nresult=ok";
        Assert.Equal(input, SecretRedactor.Redact(input));
    }
}
