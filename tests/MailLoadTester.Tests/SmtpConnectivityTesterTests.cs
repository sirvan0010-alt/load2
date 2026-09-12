using MailKit.Security;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class SmtpConnectivityTesterTests
{
    [Theory]
    [InlineData(SmtpSecurity.None, SecureSocketOptions.None)]
    [InlineData(SmtpSecurity.StartTls, SecureSocketOptions.StartTls)]
    [InlineData(SmtpSecurity.ImplicitTls, SecureSocketOptions.SslOnConnect)]
    public void ToSocketOptions_MapsSecurityMode(SmtpSecurity security, SecureSocketOptions expected)
    {
        Assert.Equal(expected, SmtpConnectivityTester.ToSocketOptions(security));
    }
}
