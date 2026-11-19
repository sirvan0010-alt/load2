using System.Security.Cryptography;
using System.Text;
using MailLoadTester;

namespace MailLoadTester.Gui;

/// <summary>
/// Protects OAuth refresh tokens for the current Windows user only.
/// Data cannot be decrypted by another Windows account or on another machine.
/// </summary>
internal sealed class WindowsDpapiSecretProtector : IGoogleOAuthSecretProtector
{
    private static readonly byte[] Entropy =
        SHA256.HashData(Encoding.UTF8.GetBytes("MailLoadTester.GoogleOAuth.RefreshToken.v1"));

    public byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] protectedData)
    {
        ArgumentNullException.ThrowIfNull(protectedData);
        return ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.CurrentUser);
    }
}
