using Xunit;
using MailLoadTester;

namespace MailLoadTester.Tests;

public sealed class EnvironmentStructuredAiPlanProviderTests
{
    [Fact]
    public async Task Provider_Rejects_Missing_Endpoint()
    {
        var previous = Environment.GetEnvironmentVariable(
            EnvironmentStructuredAiPlanProvider.EndpointVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                EnvironmentStructuredAiPlanProvider.EndpointVariable, null);

            var provider = new EnvironmentStructuredAiPlanProvider(new HttpClient());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.CreatePlanJsonAsync(
                    new AiTaskContext(CreateOptions()),
                    CancellationToken.None).AsTask());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                EnvironmentStructuredAiPlanProvider.EndpointVariable, previous);
        }
    }

    [Fact]
    public async Task Provider_Sends_No_Smtp_Password()
    {
        using var handler = new CapturingHandler();
        using var client = new HttpClient(handler);

        var previous = Environment.GetEnvironmentVariable(
            EnvironmentStructuredAiPlanProvider.EndpointVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                EnvironmentStructuredAiPlanProvider.EndpointVariable,
                "https://ai.example.test/plan");

            var provider = new EnvironmentStructuredAiPlanProvider(client);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.CreatePlanJsonAsync(
                    new AiTaskContext(CreateOptions()),
                    CancellationToken.None).AsTask());

            Assert.DoesNotContain("secret-password", handler.Body ?? string.Empty,
                StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                EnvironmentStructuredAiPlanProvider.EndpointVariable, previous);
        }
    }

    private static MailTestOptions CreateOptions() =>
        new(
            From: "tester@example.test",
            Recipients: new[] { "recipient@example.test" },
            SmtpHost: "smtp.example.test",
            Port: 587,
            Security: SmtpSecurity.StartTls,
            UseAuthentication: true,
            Username: "smtp-user",
            Password: "secret-password",
            MessageCount: 10,
            IntervalMs: 100,
            BatchMode: false,
            BatchSize: 1,
            BatchPauseSeconds: 1,
            MaxConcurrency: 2,
            Subject: "test",
            Body: "test",
            DisplayName: "test",
            RandomTestData: false,
            TestMode: true,
            AllowedDomains: "example.test",
            HtmlBody: false,
            Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(),
            IgnoreCertificateErrors: false,
            MaxRetries: 0,
            DryRun: false,
            Unauthorized: false);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(
                System.Net.HttpStatusCode.BadRequest)
            {
                Content = new StringContent("rejected")
            };
        }
    }
}
