using System.Text;
using MailLoadTester;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class GoogleOAuthCredentialStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsAccountThroughProtector()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "google.dat");
        var protector = new TestProtector();
        var store = new GoogleOAuthCredentialStore(path, protector);
        var account = new GoogleOAuthStoredAccount("google-subject", "person@gmail.com", "refresh-secret");

        try
        {
            await store.SaveAsync(account);
            var loaded = await store.LoadAsync();

            Assert.Equal(account, loaded);
            Assert.True(protector.ProtectCalls > 0);
            Assert.True(protector.UnprotectCalls > 0);
            Assert.DoesNotContain("refresh-secret", await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsNullWhenNoCredentialFileExists()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.dat");
        var store = new GoogleOAuthCredentialStore(path, new TestProtector());

        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_ReportsCorruptedProtectedPayload()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "google.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("not-json"));
        var store = new GoogleOAuthCredentialStore(path, new TestProtector());

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredCredential()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "google.dat");
        var store = new GoogleOAuthCredentialStore(path, new TestProtector());
        try
        {
            await store.SaveAsync(new GoogleOAuthStoredAccount("sub", "person@gmail.com", "refresh"));
            await store.DeleteAsync();

            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(path)!))
                Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    private sealed class TestProtector : IGoogleOAuthSecretProtector
    {
        public int ProtectCalls { get; private set; }
        public int UnprotectCalls { get; private set; }

        public byte[] Protect(byte[] plaintext)
        {
            ProtectCalls++;
            return plaintext.Select(value => (byte)(value ^ 0x5A)).ToArray();
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            UnprotectCalls++;
            return protectedData.Select(value => (byte)(value ^ 0x5A)).ToArray();
        }
    }
}
