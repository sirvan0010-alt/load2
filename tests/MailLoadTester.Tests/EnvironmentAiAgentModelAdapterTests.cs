using System.Net;
using System.Text;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class EnvironmentAiAgentModelAdapterTests
{
    [Fact]
    public async Task MissingEndpoint_IsRejected()
    {
        var old = Environment.GetEnvironmentVariable(EnvironmentAiAgentModelAdapter.EndpointVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                EnvironmentAiAgentModelAdapter.EndpointVariable, null);

            using var adapter = new EnvironmentAiAgentModelAdapter(
                new HttpClient(new StubHandler()));

            var request = CreateRequest();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                adapter.GenerateAsync(request, CancellationToken.None));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                EnvironmentAiAgentModelAdapter.EndpointVariable, old);
        }
    }

    [Fact]
    public async Task RequestContainsOnlyNonSecretTaskEnvelope()
    {
        string? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    \"{\\\"summary\\\":\\\"proposal\\\",\\\"proposedChanges\\\":[],\\\"proposedTests\\\":[],\\\"evidence\\\":[\\\"observation\\\"],\\\"requiresIndependentVerification\\\":true}\",
                    Encoding.UTF8,
                    \"application/json\")
            };
        });

        var oldEndpoint = Environment.GetEnvironmentVariable(
            EnvironmentAiAgentModelAdapter.EndpointVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                EnvironmentAiAgentModelAdapter.EndpointVariable,
                \"https://example.invalid/agent\");

            using var adapter = new EnvironmentAiAgentModelAdapter(
                new HttpClient(handler));

            var response = await adapter.GenerateAsync(
                CreateRequest(),
                CancellationToken.None);

            Assert.Equal(\"proposal\", response.Summary);
            Assert.NotNull(captured);
            Assert.Contains(\"Task-001\", captured);
            Assert.DoesNotContain(\"smtp-password\", captured);
            Assert.DoesNotContain(\"AuthorizationToken\", captured);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                EnvironmentAiAgentModelAdapter.EndpointVariable,
                oldEndpoint);
        }
    }

    private static AiAgentModelRequest CreateRequest() => new(
        \"Task-001\",
        \"developer\",
        \"sirvan0010-alt/load2\",
        new string('a', 40),
        new[] { \"src/MailLoadTester.Core\", \"tests/MailLoadTester.Tests\" },
        new[] { \"tests pass\", \"CI verifies the change\" },
        1,
        DateTimeOffset.UtcNow.AddMinutes(1));

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage>? _response;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage>? response = null)
            => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(_response?.Invoke(request) ?? new HttpResponseMessage(
                HttpStatusCode.OK)
            {
                Content = new StringContent(
                    \"{\\\"summary\\\":\\\"ok\\\",\\\"proposedChanges\\\":[],\\\"proposedTests\\\":[],\\\"evidence\\\":[],\\\"requiresIndependentVerification\\\":true}\",
                    Encoding.UTF8,
                    \"application/json\")
            });
        }
    }
}
