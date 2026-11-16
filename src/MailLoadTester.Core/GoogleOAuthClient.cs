using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MailLoadTester;

/// <summary>
/// Google OAuth 2.0 primitives for a native desktop client. This class deliberately
/// does not accept or send a client secret: installed desktop applications are public clients.
/// </summary>
public sealed class GoogleOAuthClient
{
    public const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    public const string UserInfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
    public const string GmailSendScope = "https://www.googleapis.com/auth/gmail.send";
    public const string DefaultScopes = "openid email profile " + GmailSendScope;

    private readonly HttpClient _http;
    private readonly string _clientId;

    public GoogleOAuthClient(string clientId, HttpClient httpClient)
    {
        if (string.IsNullOrWhiteSpace(clientId)
            || !clientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal))
            throw new ArgumentException("Google OAuth Client ID není platný desktop Client ID.", nameof(clientId));

        _clientId = clientId;
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public static GoogleOAuthPkceChallenge CreatePkceChallenge()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        return new GoogleOAuthPkceChallenge(verifier, challenge, state);
    }

    public Uri BuildAuthorizationUri(
        Uri loopbackRedirectUri,
        GoogleOAuthPkceChallenge pkce,
        string scopes = DefaultScopes,
        bool forceConsent = false)
    {
        ArgumentNullException.ThrowIfNull(loopbackRedirectUri);
        ArgumentNullException.ThrowIfNull(pkce);

        if (!IsLoopbackRedirect(loopbackRedirectUri))
            throw new ArgumentException("OAuth redirect URI musí používat HTTP loopback 127.0.0.1 nebo [::1].", nameof(loopbackRedirectUri));
        if (string.IsNullOrWhiteSpace(pkce.CodeVerifier)
            || string.IsNullOrWhiteSpace(pkce.CodeChallenge)
            || string.IsNullOrWhiteSpace(pkce.State))
            throw new ArgumentException("PKCE verifier, challenge a state nesmí být prázdné.", nameof(pkce));
        if (string.IsNullOrWhiteSpace(scopes))
            throw new ArgumentException("OAuth scopes nesmí být prázdné.", nameof(scopes));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["redirect_uri"] = loopbackRedirectUri.ToString(),
            ["response_type"] = "code",
            ["scope"] = scopes,
            ["access_type"] = "offline",
            // Avoid creating a fresh refresh token on every ordinary sign-in.
            // Force consent only when a user explicitly needs to re-authorize.
            ["prompt"] = forceConsent ? "consent select_account" : "select_account",
            ["state"] = pkce.State,
            ["code_challenge"] = pkce.CodeChallenge,
            ["code_challenge_method"] = "S256"
        };
        var encoded = string.Join("&", query.Select(p =>
            Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));
        return new Uri(AuthorizationEndpoint + "?" + encoded, UriKind.Absolute);
    }

    public async Task<GoogleOAuthTokenSet> ExchangeCodeAsync(
        string authorizationCode,
        Uri loopbackRedirectUri,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
            throw new ArgumentException("Authorization code nesmí být prázdný.", nameof(authorizationCode));
        if (!IsLoopbackRedirect(loopbackRedirectUri))
            throw new ArgumentException("OAuth redirect URI musí být HTTP loopback.", nameof(loopbackRedirectUri));
        if (string.IsNullOrWhiteSpace(codeVerifier))
            throw new ArgumentException("PKCE code verifier nesmí být prázdný.", nameof(codeVerifier));

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["code"] = authorizationCode,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = loopbackRedirectUri.ToString()
        });
        return await PostTokenAsync(content, previousRefreshToken: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GoogleOAuthTokenSet> RefreshAsync(
        GoogleOAuthTokenSet current,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (string.IsNullOrWhiteSpace(current.RefreshToken))
            throw new InvalidOperationException("Pro tento Google účet není uložen obnovovací token.");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["refresh_token"] = current.RefreshToken,
            ["grant_type"] = "refresh_token"
        });
        return await PostTokenAsync(content, current.RefreshToken, cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Google refresh token nesmí být prázdný.", nameof(refreshToken));

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = refreshToken
        });
        using var response = await _http.PostAsync(
            "https://oauth2.googleapis.com/revoke", content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
            throw new GoogleOAuthException("Google se nepodařilo požádat o odvolání přístupu.", response.StatusCode);
    }

    public async Task<GoogleOAuthUser> GetUserAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Access token nesmí být prázdný.", nameof(accessToken));

        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new GoogleOAuthException("Google nevrátil informace o přihlášeném účtu.", response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<GoogleOAuthUserInfo>(
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email) || user.EmailVerified != true)
            throw new GoogleOAuthException("Google účet nemá ověřenou e-mailovou adresu.", response.StatusCode);

        return new GoogleOAuthUser(user.Subject ?? "", user.Email, user.Name ?? "");
    }

    private async Task<GoogleOAuthTokenSet> PostTokenAsync(
        FormUrlEncodedContent content,
        string? previousRefreshToken,
        CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync(TokenEndpoint, content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // Google returns a small OAuth error object (error/error_description).
            // Surface only these bounded diagnostic fields; never echo request data,
            // authorization codes, tokens, or the raw response body.
            var (errorCode, errorDescription) = await ReadSafeOAuthErrorAsync(
                response, cancellationToken).ConfigureAwait(false);
            var detail = string.IsNullOrWhiteSpace(errorCode)
                ? $"HTTP {(int)response.StatusCode}"
                : $"HTTP {(int)response.StatusCode}, {errorCode}";
            if (!string.IsNullOrWhiteSpace(errorDescription))
                detail += ": " + errorDescription;
            throw new GoogleOAuthException(
                "Google OAuth token endpoint odmítl požadavek (" + detail + ").",
                response.StatusCode, errorCode, errorDescription);
        }

        var token = await response.Content.ReadFromJsonAsync<GoogleOAuthTokenResponse>(
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken) || token.ExpiresIn <= 0)
            throw new GoogleOAuthException("Google OAuth vrátil neúplnou odpověď token endpointu.", response.StatusCode);

        var refreshToken = string.IsNullOrWhiteSpace(token.RefreshToken)
            ? previousRefreshToken
            : token.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new GoogleOAuthException("Google nevydal refresh token. Přihlášení je nutné zopakovat s offline access.", response.StatusCode);

        return new GoogleOAuthTokenSet(
            token.AccessToken,
            refreshToken,
            DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn),
            token.TokenType ?? "Bearer",
            token.Scope ?? "");
    }

    private static async Task<(string? Error, string? Description)> ReadSafeOAuthErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(
                stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return (null, null);

            static string? ReadBoundedString(JsonElement root, string property, int maxLength)
            {
                if (!root.TryGetProperty(property, out var value)
                    || value.ValueKind != JsonValueKind.String)
                    return null;
                var text = value.GetString();
                if (string.IsNullOrWhiteSpace(text))
                    return null;
                var safe = new string(text
                    .Where(ch => !char.IsControl(ch))
                    .Take(maxLength)
                    .ToArray());
                return string.IsNullOrWhiteSpace(safe) ? null : safe;
            }

            return (
                ReadBoundedString(document.RootElement, "error", 80),
                ReadBoundedString(document.RootElement, "error_description", 240));
        }
        catch (JsonException)
        {
            return (null, null);
        }
        catch (NotSupportedException)
        {
            return (null, null);
        }
    }

    private static bool IsLoopbackRedirect(Uri uri) =>
        uri.IsAbsoluteUri
        && uri.Scheme == Uri.UriSchemeHttp
        && IPAddress.TryParse(uri.Host, out var address)
        && IPAddress.IsLoopback(address)
        && uri.Port is > 0 and <= 65535;

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class GoogleOAuthTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
        [JsonPropertyName("scope")] public string? Scope { get; set; }
    }

    private sealed class GoogleOAuthUserInfo
    {
        [JsonPropertyName("sub")] public string? Subject { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("email_verified")] public bool? EmailVerified { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}

public sealed record GoogleOAuthPkceChallenge(string CodeVerifier, string CodeChallenge, string State);

public sealed record GoogleOAuthTokenSet(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string TokenType,
    string Scope)
{
    // Records generate a ToString() containing every property by default. Tokens
    // must remain redacted even if a future diagnostic accidentally formats this object.
    public override string ToString() =>
        $"GoogleOAuthTokenSet {{ AccessToken = [REDACTED], RefreshToken = [REDACTED], ExpiresAt = {ExpiresAt:O}, TokenType = {TokenType}, Scope = {Scope} }}";
}

public sealed record GoogleOAuthUser(string Subject, string Email, string Name);

public sealed class GoogleOAuthException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? OAuthError { get; }
    public string? OAuthErrorDescription { get; }

    public GoogleOAuthException(
        string message,
        HttpStatusCode statusCode,
        string? oauthError = null,
        string? oauthErrorDescription = null)
        : base(message)
    {
        StatusCode = statusCode;
        OAuthError = oauthError;
        OAuthErrorDescription = oauthErrorDescription;
    }
}
