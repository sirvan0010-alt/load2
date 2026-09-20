using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MailLoadTester.Core;

public sealed class EnvironmentAiAgentModelAdapter : IAiAgentModelAdapter, IDisposable
{
    public const string EndpointVariable = "LOAD2_AI_AGENT_ENDPOINT";
    public const string ApiKeyVariable = "LOAD2_AI_AGENT_API_KEY";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public EnvironmentAiAgentModelAdapter(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsClient = httpClient is null;

        if (_ownsClient)
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<AiAgentModelResponse> GenerateAsync(
        AiAgentModelRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException(
                $\"{EndpointVariable} is not configured. AI agent execution is optional.\");

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException(
                $\"{EndpointVariable} must be an absolute HTTP(S) URL.\");
        }

        var envelope = new AiAgentModelEnvelope(
            request.TaskId,
            request.AgentRole,
            request.Repository,
            request.ImmutableCommit,
            request.AllowedScopes.ToArray(),
            request.AcceptanceCriteria.ToArray(),
            request.Iteration,
            request.Deadline);

        using var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(envelope),
                Encoding.UTF8,
                \"application/json\")
        };

        var apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
        if (!string.IsNullOrWhiteSpace(apiKey))
            message.Headers.Authorization =
                new AuthenticationHeaderValue(\"Bearer\", apiKey);

        using var response = await _httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $\"AI agent endpoint returned HTTP {(int)response.StatusCode}.\");

        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException(
                \"AI agent endpoint returned an empty response.\");

        try
        {
            var result = JsonSerializer.Deserialize<AiAgentModelResponse>(
                body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return result ?? throw new InvalidOperationException(
                \"AI agent endpoint returned an invalid response.\");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                \"AI agent endpoint returned malformed JSON.\", ex);
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
            _httpClient.Dispose();
    }

    private sealed record AiAgentModelEnvelope(
        string TaskId,
        string AgentRole,
        string Repository,
        string ImmutableCommit,
        IReadOnlyList<string> AllowedScopes,
        IReadOnlyList<string> AcceptanceCriteria,
        int Iteration,
        DateTimeOffset Deadline);
}
