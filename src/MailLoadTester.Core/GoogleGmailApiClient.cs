using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MimeKit;

namespace MailLoadTester;

/// <summary>
/// Sends a single MIME message using the Gmail API. This is deliberately separate
/// from the SMTP runner and its protocol metrics.
/// </summary>
public sealed class GoogleGmailApiClient
{
    public const string SendEndpoint = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";

    private readonly HttpClient _http;

    public GoogleGmailApiClient(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<GoogleGmailSendResult> SendAsync(
        MimeMessage message,
        GoogleOAuthClient oauth,
        GoogleOAuthTokenSet tokens,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(oauth);
        ArgumentNullException.ThrowIfNull(tokens);
        if (string.IsNullOrWhiteSpace(tokens.AccessToken))
            throw new ArgumentException("Google access token nesmí být prázdný.", nameof(tokens));

        var raw = SerializeMimeBase64Url(message);
        var response = await SendRequestAsync(raw, tokens.AccessToken, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            // A 401 explicitly means the bearer token was rejected. Refresh and retry
            // once only. Network failures and all other HTTP statuses are never retried.
            tokens = await oauth.RefreshAsync(tokens, cancellationToken).ConfigureAwait(false);
            response = await SendRequestAsync(raw, tokens.AccessToken, cancellationToken).ConfigureAwait(false);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new GoogleGmailApiException(
                    "Gmail API odmítlo odeslání zprávy. HTTP " + (int)response.StatusCode + ".",
                    response.StatusCode);

            var sent = await response.Content.ReadFromJsonAsync<GoogleGmailSendResponse>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (sent is null || string.IsNullOrWhiteSpace(sent.Id))
                throw new GoogleGmailApiException(
                    "Gmail API nevrátilo identifikátor odeslané zprávy.",
                    response.StatusCode);

            return new GoogleGmailSendResult(sent.Id, sent.ThreadId ?? string.Empty, tokens);
        }
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        string raw,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new GoogleGmailSendRequest(raw));
        return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static string SerializeMimeBase64Url(MimeMessage message)
    {
        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return Convert.ToBase64String(stream.ToArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed record GoogleGmailSendRequest(
        [property: JsonPropertyName("raw")] string Raw);

    private sealed class GoogleGmailSendResponse
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("threadId")] public string? ThreadId { get; set; }
    }
}

public sealed record GoogleGmailSendResult(
    string MessageId,
    string ThreadId,
    GoogleOAuthTokenSet Tokens);

public sealed class GoogleGmailApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public GoogleGmailApiException(string message, HttpStatusCode statusCode)
        : base(message) => StatusCode = statusCode;
}
