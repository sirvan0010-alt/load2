using System.Net.Http.Json;
using System.Text.Json;

namespace MailLoadTester.Core;

/// <summary>
/// Provider adapter for the OpenHands Agent Server REST API.
/// Credentials are supplied by the caller; this type never reads or stores them in source.
/// </summary>
public sealed class OpenHandsAgentExecutionBackend : IAgentExecutionBackend
{
    private readonly HttpClient _httpClient;
    private readonly string? _sessionApiKey;
    private readonly string _model;
    private readonly TimeSpan _pollInterval;

    public OpenHandsAgentExecutionBackend(
        HttpClient httpClient,
        string? sessionApiKey,
        string model = "gpt-5.5",
        TimeSpan? pollInterval = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _sessionApiKey = string.IsNullOrWhiteSpace(sessionApiKey) ? null : sessionApiKey;
        _model = string.IsNullOrWhiteSpace(model) ? throw new ArgumentException("Model is required.", nameof(model)) : model;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
        if (_pollInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
    }

    public string Name => "openhands-agent-server";

    public async Task<AgentExecutionResult> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = BuildPrompt(request);
        using var create = new HttpRequestMessage(HttpMethod.Post, "api/conversations")
        {
            Content = JsonContent.Create(new
            {
                agent = new { kind = "Agent", llm = new { model = _model }, tools = new[] { "terminal", "file_editor", "task_tracker" } },
                workspace = new { working_dir = Path.GetFullPath(request.WorkspacePath) },
                initial_message = new { role = "user", content = new[] { new { type = "text", text = prompt } }, run = true }
            })
        };

        AddAuthentication(create);
        using var response = await _httpClient.SendAsync(create, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var conversationId = document.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new InvalidOperationException("OpenHands did not return a conversation id.");

        var started = DateTimeOffset.UtcNow;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow - started > request.TimeBudget)
            {
                await TryDeleteConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
                return new AgentExecutionResult(AgentExecutionStatus.TimedOut, request.TaskId, null,
                    Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                    new[] { "OpenHands execution exceeded the task time budget." }, false, false, 0,
                    DateTimeOffset.UtcNow - started);
            }

            var state = await GetConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
            if (state.Status is "finished" or "error" or "stuck")
            {
                var finalResponse = await GetFinalResponseAsync(conversationId, cancellationToken).ConfigureAwait(false);
                var completed = state.Status == "finished";
                return new AgentExecutionResult(completed ? AgentExecutionStatus.Completed : AgentExecutionStatus.Failed,
                    request.TaskId, null, Array.Empty<string>(), Array.Empty<string>(),
                    string.IsNullOrWhiteSpace(finalResponse) ? Array.Empty<string>() : new[] { finalResponse },
                    completed ? Array.Empty<string>() : new[] { $"OpenHands execution status: {state.Status}." },
                    completed, false, 0, DateTimeOffset.UtcNow - started);
            }

            await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ConversationState> GetConversationAsync(string id, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/conversations/{Uri.EscapeDataString(id)}");
        AddAuthentication(request);
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        var status = document.RootElement.TryGetProperty("execution_status", out var value) ? value.GetString() ?? "unknown" : "unknown";
        return new ConversationState(status);
    }

    private async Task<string?> GetFinalResponseAsync(string id, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/conversations/{Uri.EscapeDataString(id)}/agent_final_response");
        AddAuthentication(request);
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return document.RootElement.TryGetProperty("response", out var value) ? value.GetString() : null;
    }

    private async Task TryDeleteConversationAsync(string id, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/conversations/{Uri.EscapeDataString(id)}");
        AddAuthentication(request);
        try { await _httpClient.SendAsync(request, ct).ConfigureAwait(false); }
        catch (HttpRequestException) { }
    }

    private void AddAuthentication(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_sessionApiKey)) request.Headers.Add("X-Session-API-Key", _sessionApiKey);
    }

    private static string BuildPrompt(AgentExecutionRequest request) =>
        $"Task ID: {request.TaskId}\nRole: {request.AgentRole}\nRepository: {request.Repository}\n" +
        $"Immutable commit: {request.ImmutableCommit}\nAllowed scopes:\n- {string.Join("\n- ", request.AllowedScopes)}\n" +
        $"Acceptance criteria:\n- {string.Join("\n- ", request.AcceptanceCriteria)}\nMaximum iterations: {request.MaxIterations}\n" +
        "Work only inside the supplied workspace. Do not access real targets unless explicitly authorized. " +
        "Do not expose credentials or secrets. Run the required tests and report results accurately.";

    private sealed record ConversationState(string Status);
}
