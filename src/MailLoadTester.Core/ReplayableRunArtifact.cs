using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MailLoadTester;

/// <summary>
/// B8: deterministic, redacted run evidence suitable for replay in a controlled lab.
/// The artifact is data only; it never performs network I/O or retries execution.
/// </summary>
public sealed record ReplayableRunArtifact(
    int SchemaVersion,
    string RunId,
    DateTimeOffset CreatedUtc,
    JsonObject Scenario,
    JsonObject Configuration,
    JsonArray Events,
    JsonNode Result,
    string Sha256);

public static class ReplayableRunArtifactBuilder
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ReplayableRunArtifact Create(
        string runId,
        DateTimeOffset createdUtc,
        object scenario,
        object configuration,
        IEnumerable<object>? events,
        object result)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("RunId is required.", nameof(runId));

        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(result);

        var scenarioNode = Redact(JsonSerializer.SerializeToNode(scenario, JsonOptions)) as JsonObject
            ?? throw new ArgumentException("Scenario must serialize to an object.", nameof(scenario));
        var configurationNode = Redact(JsonSerializer.SerializeToNode(configuration, JsonOptions)) as JsonObject
            ?? throw new ArgumentException("Configuration must serialize to an object.", nameof(configuration));
        var eventArray = new JsonArray();
        if (events is not null)
        {
            foreach (var item in events)
            {
                ArgumentNullException.ThrowIfNull(item);
                eventArray.Add(Redact(JsonSerializer.SerializeToNode(item, JsonOptions)));
            }
        }

        var resultNode = Redact(JsonSerializer.SerializeToNode(result, JsonOptions))
            ?? throw new ArgumentException("Result cannot serialize to null.", nameof(result));

        var unsigned = new JsonObject
        {
            ["schemaVersion"] = CurrentSchemaVersion,
            ["runId"] = runId,
            ["createdUtc"] = createdUtc.ToUniversalTime().ToString("O"),
            ["scenario"] = scenarioNode,
            ["configuration"] = configurationNode,
            ["events"] = eventArray,
            ["result"] = resultNode
        };

        var canonical = unsigned.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        return new ReplayableRunArtifact(
            CurrentSchemaVersion,
            runId,
            createdUtc.ToUniversalTime(),
            scenarioNode,
            configurationNode,
            eventArray,
            resultNode,
            hash);
    }

    public static string ToJson(ReplayableRunArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return JsonSerializer.Serialize(artifact, JsonOptions);
    }

    public static async Task<string> WriteAsync(
        ReplayableRunArtifact artifact,
        string directory,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Artifact directory is required.", nameof(directory));

        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        var safeRunId = string.Concat(artifact.RunId.Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_'));
        if (string.IsNullOrWhiteSpace(safeRunId))
            safeRunId = "run";

        var finalPath = Path.Combine(directory, $"{safeRunId}.json");
        var tempPath = finalPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllTextAsync(tempPath, ToJson(artifact), new UTF8Encoding(false), ct);
            ct.ThrowIfCancellationRequested();
            File.Move(tempPath, finalPath, overwrite: true);
            return finalPath;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best effort cleanup */ }
        }
    }

    private static JsonNode? Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (IsSecretName(property.Key))
                    obj[property.Key] = "[REDACTED]";
                else
                    obj[property.Key] = Redact(property.Value);
            }
            return obj;
        }

        if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
                array[i] = Redact(array[i]);
        }
        return node;
    }

    private static bool IsSecretName(string name)
    {
        var n = name.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        return n.Contains("password", StringComparison.OrdinalIgnoreCase)
            || n.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || n.Contains("token", StringComparison.OrdinalIgnoreCase)
            || n.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || n.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || n.Contains("cookie", StringComparison.OrdinalIgnoreCase)
            || n.Contains("privatekey", StringComparison.OrdinalIgnoreCase)
            || n.Contains("clientcertificate", StringComparison.OrdinalIgnoreCase);
    }
}
