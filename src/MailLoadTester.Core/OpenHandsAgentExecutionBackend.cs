using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

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
                var evidence = await GetExecutionEvidenceAsync(conversationId, cancellationToken).ConfigureAwait(false);
                var completed = state.Status == "finished";
                var diagnostics = evidence.Diagnostics.ToList();
                if (!completed)
                    diagnostics.Add($"OpenHands execution status: {state.Status}.");
                if (!evidence.TestsPassed)
                    diagnostics.Add("Required test success was not established from an observed test command/exit code pair.");

                return new AgentExecutionResult(completed ? AgentExecutionStatus.Completed : AgentExecutionStatus.Failed,
                    request.TaskId, evidence.Commit, evidence.ChangedFiles, evidence.Commands,
                    evidence.Evidence.Concat(string.IsNullOrWhiteSpace(finalResponse) ? Array.Empty<string>() : new[] { finalResponse }).ToArray(),
                    diagnostics, evidence.TestsPassed, evidence.SecurityPassed, evidence.Iterations,
                    DateTimeOffset.UtcNow - started);
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

    private async Task<ExecutionEvidence> GetExecutionEvidenceAsync(string id, CancellationToken ct)
    {
        var commands = new List<string>();
        var changedFiles = new HashSet<string>(StringComparer.Ordinal);
        var evidence = new List<string>();
        var diagnostics = new List<string>();
        var testsPassed = false;
        var iterations = 0;
        string? commit = null;
        string? pageId = null;

        for (var page = 0; page < 10; page++)
        {
            var suffix = $"?limit=100{(string.IsNullOrWhiteSpace(pageId) ? string.Empty : $"&page_id={Uri.EscapeDataString(pageId)}")}";
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"api/conversations/{Uri.EscapeDataString(id)}/events/search{suffix}");
            AddAuthentication(request);
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));

            if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                break;

            foreach (var item in items.EnumerateArray())
                ExtractObservedEvent(item, commands, changedFiles, evidence, diagnostics, ref testsPassed, ref iterations, ref commit);

            if (!document.RootElement.TryGetProperty("next_page_id", out var next) || next.ValueKind != JsonValueKind.String)
                break;
            pageId = next.GetString();
            if (string.IsNullOrWhiteSpace(pageId))
                break;
        }

        return new ExecutionEvidence(
            commit,
            changedFiles.Order(StringComparer.Ordinal).ToArray(),
            commands.Distinct(StringComparer.Ordinal).Take(100).ToArray(),
            evidence.Distinct(StringComparer.Ordinal).Take(100).ToArray(),
            diagnostics.Distinct(StringComparer.Ordinal).Take(100).ToArray(),
            testsPassed,
            false,
            iterations);
    }

    private static void ExtractObservedEvent(
        JsonElement item,
        List<string> commands,
        HashSet<string> changedFiles,
        List<string> evidence,
        List<string> diagnostics,
        ref bool testsPassed,
        ref int iterations,
        ref string? commit)
    {
        var kind = GetString(item, "kind") ?? GetString(item, "type") ?? string.Empty;
        var action = GetString(item, "action") ?? string.Empty;
        var command = FindString(item, "command");
        var path = FindString(item, "path") ?? FindString(item, "file_path");
        var exitCode = FindInt(item, "exit_code");

        if (kind.Contains("Action", StringComparison.OrdinalIgnoreCase))
            iterations++;

        if (!string.IsNullOrWhiteSpace(command) &&
            (kind.Contains("CmdRun", StringComparison.OrdinalIgnoreCase) ||
             action.Contains("run", StringComparison.OrdinalIgnoreCase)))
        {
            var safeCommand = Redact(command);
            commands.Add(safeCommand);
            if (IsTestCommand(command) && exitCode == 0)
                testsPassed = true;
            evidence.Add("Observed command: " + safeCommand);
        }

        if (!string.IsNullOrWhiteSpace(path) &&
            (kind.Contains("FileEdit", StringComparison.OrdinalIgnoreCase) ||
             kind.Contains("FileWrite", StringComparison.OrdinalIgnoreCase) ||
             kind.Contains("FileDelete", StringComparison.OrdinalIgnoreCase) ||
             action.Contains("file", StringComparison.OrdinalIgnoreCase)))
        {
            changedFiles.Add(path);
            evidence.Add("Observed file action: " + path);
        }

        if (exitCode is not null && exitCode != 0)
            diagnostics.Add($"Observed command exit code: {exitCode.Value}");

        if (kind.Contains("Error", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add("Observed OpenHands error event.");

        var observedCommit = FindString(item, "commit") ?? FindString(item, "commit_sha");
        if (!string.IsNullOrWhiteSpace(observedCommit) && observedCommit.Length == 40)
            commit = observedCommit;
    }

    private static bool IsTestCommand(string command) =>
        Regex.IsMatch(command, @"(^|\s)(dotnet\s+test|dotnet\s+build|pytest|npm\s+(test|run\s+test)|cargo\s+test)(\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? FindInt(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var direct) && direct.ValueKind == JsonValueKind.Number && direct.TryGetInt32(out var value))
            return value;
        foreach (var child in EnumerateChildren(element))
        {
            var found = FindInt(child, property);
            if (found is not null) return found;
        }
        return null;
    }

    private static string? FindString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var direct) && direct.ValueKind == JsonValueKind.String)
            return direct.GetString();
        foreach (var child in EnumerateChildren(element))
        {
            var found = FindString(child, property);
            if (!string.IsNullOrWhiteSpace(found)) return found;
        }
        return null;
    }

    private static IEnumerable<JsonElement> EnumerateChildren(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
                yield return property.Value;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                yield return child;
        }
    }

    private static string Redact(string value) =>
        Regex.Replace(value,
            @"(?i)(password|secret|token|api[_-]?key|authorization|cookie)\s*[=:]\s*[^\s]+",
            "$1=[REDACTED]",
            RegexOptions.CultureInvariant);

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
        (string.IsNullOrWhiteSpace(request.RepairFeedback) ? string.Empty : $"Repair feedback from the previous attempt:\n{request.RepairFeedback}\n") +
        "Work only inside the supplied workspace. Do not access real targets unless explicitly authorized. " +
        "Do not expose credentials or secrets. Run the required tests and report results accurately.";

    private sealed record ConversationState(string Status);

    private sealed record ExecutionEvidence(
        string? Commit,
        IReadOnlyList<string> ChangedFiles,
        IReadOnlyList<string> Commands,
        IReadOnlyList<string> Evidence,
        IReadOnlyList<string> Diagnostics,
        bool TestsPassed,
        bool SecurityPassed,
        int Iterations);
}
