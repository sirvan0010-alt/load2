using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MailLoadTester;

/// <summary>
/// HTTP-backed structured-plan provider. The endpoint is intentionally provider-neutral:
/// it receives a sanitized task envelope and must return an ExecutionPlan JSON document.
/// Secrets from MailTestOptions are never serialized.
/// </summary>
public sealed class EnvironmentStructuredAiPlanProvider : IStructuredAiPlanProvider, IDisposable
{
    public const string EndpointVariable = "LOAD2_AI_PLAN_ENDPOINT";
    public const string ApiKeyVariable = "LOAD2_AI_API_KEY";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public EnvironmentStructuredAiPlanProvider(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsClient = httpClient is null;
        if (_ownsClient)
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async ValueTask<string> CreatePlanJsonAsync(
        AiTaskContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException(
                $"{EndpointVariable} není nastavená. AI plánování je volitelné a bez endpointu se nepouští.");

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException($"{EndpointVariable} musí být absolutní HTTP(S) URL.");

        var request = new AiPlanRequest(
            context.Options.SmtpHost,
            context.Options.Port,
            context.Options.DirectMxDelivery,
            context.Options.MessageCount,
            context.Options.MaxConcurrency,
            context.Options.DurationSeconds,
            GetTargets(context),
            context.AllowedTargets?.ToArray() ?? Array.Empty<string>());

        using var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json")
        };

        var apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
        if (!string.IsNullOrWhiteSpace(apiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"AI plan endpoint returned HTTP {(int)response.StatusCode}.");

        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("AI plan endpoint returned an empty response.");

        return body;