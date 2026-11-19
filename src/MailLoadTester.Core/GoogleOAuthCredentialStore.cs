using System.Security.Cryptography;
using System.Text.Json;

namespace MailLoadTester;

public interface IGoogleOAuthSecretProtector
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] protectedData);
}

public sealed record GoogleOAuthStoredAccount(
    string Subject,
    string Email,
    string RefreshToken)
{
    public override string ToString() =>
        $"GoogleOAuthStoredAccount {{ Subject = {Subject}, Email = {Email}, RefreshToken = [REDACTED] }}";
}

/// <summary>
/// Stores only the minimum Google account metadata and refresh token in an
/// encrypted per-user file. The caller supplies the platform-specific protector.
/// </summary>
public sealed class GoogleOAuthCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly string _filePath;
    private readonly IGoogleOAuthSecretProtector _protector;

    public GoogleOAuthCredentialStore(string filePath, IGoogleOAuthSecretProtector protector)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Cesta k úložišti Google OAuth nesmí být prázdná.", nameof(filePath));

        _filePath = Path.GetFullPath(filePath);
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task SaveAsync(
        GoogleOAuthStoredAccount account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (string.IsNullOrWhiteSpace(account.Subject)
            || string.IsNullOrWhiteSpace(account.Email)
            || string.IsNullOrWhiteSpace(account.RefreshToken))
            throw new ArgumentException("Google účet musí obsahovat ověřený identifikátor, e-mail a refresh token.", nameof(account));

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(account, JsonOptions);
        byte[]? protectedBytes = null;
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Nelze určit adresář úložiště Google OAuth.");
        var temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            protectedBytes = _protector.Protect(plaintext);
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(temporaryPath, protectedBytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (protectedBytes is not null)
                CryptographicOperations.ZeroMemory(protectedBytes);
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
                // Best-effort cleanup only; the original exception must remain visible.
            }
        }
    }

    public async Task<GoogleOAuthStoredAccount?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return null;

        var protectedBytes = await File.ReadAllBytesAsync(_filePath, cancellationToken).ConfigureAwait(false);
        byte[]? plaintext = null;
        try
        {
            plaintext = _protector.Unprotect(protectedBytes);
            var account = JsonSerializer.Deserialize<GoogleOAuthStoredAccount>(plaintext, JsonOptions);
            if (account is null
                || string.IsNullOrWhiteSpace(account.Subject)
                || string.IsNullOrWhiteSpace(account.Email)
                || string.IsNullOrWhiteSpace(account.RefreshToken))
                throw new InvalidDataException("Uložené přihlašovací údaje Google jsou neúplné.");

            return account;
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or FormatException)
        {
            throw new InvalidDataException(
                "Uložené přihlašovací údaje Google nelze bezpečně přečíst. Odpojte účet a přihlaste se znovu.",
                ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            if (plaintext is not null)
                CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(_filePath))
            File.Delete(_filePath);
        return Task.CompletedTask;
    }
}
