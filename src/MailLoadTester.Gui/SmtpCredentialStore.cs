using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MailLoadTester.Gui;

/// <summary>
/// Optional SMTP credential persistence protected for the current Windows user via DPAPI.
/// The password is never written to a profile JSON file.
/// </summary>
internal sealed class SmtpCredentialStore
{
    private static readonly byte[] Entropy =
        SHA256.HashData(Encoding.UTF8.GetBytes("MailLoadTester.SmtpCredential.v1"));

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MailLoadTester", "smtp-credential.dat");

    internal sealed record Credentials(
        string Host,
        int Port,
        string Username,
        string Password,
        string Security,
        string AuthMethod);

    public async Task SaveAsync(Credentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (string.IsNullOrWhiteSpace(credentials.Host) ||
            string.IsNullOrWhiteSpace(credentials.Username) ||
            string.IsNullOrEmpty(credentials.Password) ||
            credentials.Port is < 1 or > 65535)
            throw new ArgumentException("SMTP údaje nejsou úplné.");

        var clear = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(credentials));
        byte[]? protectedBytes = null;
        var path = FilePath;
        var directory = Path.GetDirectoryName(path)!;
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            protectedBytes = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(tempPath, protectedBytes, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clear);
            if (protectedBytes is not null) CryptographicOperations.ZeroMemory(protectedBytes);
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    public Credentials? Load()
    {
        var path = FilePath;
        if (!File.Exists(path)) return null;

        var protectedBytes = File.ReadAllBytes(path);
        byte[]? clear = null;
        try
        {
            clear = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            var credentials = JsonSerializer.Deserialize<Credentials>(clear);
            if (credentials is null ||
                string.IsNullOrWhiteSpace(credentials.Host) ||
                string.IsNullOrWhiteSpace(credentials.Username) ||
                string.IsNullOrEmpty(credentials.Password) ||
                credentials.Port is < 1 or > 65535)
                throw new InvalidDataException("Uložené SMTP přihlašovací údaje jsou neplatné.");
            return credentials;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            if (clear is not null) CryptographicOperations.ZeroMemory(clear);
        }
    }

    public void Delete()
    {
        var path = FilePath;
        if (File.Exists(path)) File.Delete(path);
    }
}
