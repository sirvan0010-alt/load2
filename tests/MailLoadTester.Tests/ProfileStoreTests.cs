using Xunit;

namespace MailLoadTester.Tests;

public sealed class ProfileStoreTests
{
    [Fact]
    public async Task SaveAsync_OmitsSecretsByDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mlt-profile-{Guid.NewGuid():N}.json");
        try
        {
            await ProfileStore.SaveAsync(path, CreateOptions());

            var loaded = await ProfileStore.LoadAsync(path);
            Assert.Equal(string.Empty, loaded.Options.Password);
            Assert.Equal(string.Empty, loaded.Options.ProxyPassword);
            Assert.Equal(string.Empty, loaded.Options.ClientCertificatePassword);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task SaveAsync_PersistsSecretsOnlyWhenExplicitlyRequested()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mlt-profile-{Guid.NewGuid():N}.json");
        try
        {
            await ProfileStore.SaveAsync(path, CreateOptions(), includeSecrets: true);

            var loaded = await ProfileStore.LoadAsync(path);
            Assert.Equal("smtp-secret", loaded.Options.Password);
            Assert.Equal("proxy-secret", loaded.Options.ProxyPassword);
            Assert.Equal("certificate-secret", loaded.Options.ClientCertificatePassword);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task SaveAndLoad_PreservesCapacityRunLimits()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mlt-profile-{Guid.NewGuid():N}.json");
        var options = CreateOptions() with
        {
            CapacityRun = new CapacityRunLimits(
                Enabled: true,
                MaxMessageCount: 321,
                TargetMimeBytes: 12_345_678,
                MaxMimeMessageBytes: 1_234_567,
                MaxMessagesPerMinute: 42,
                ValidateBeforeRun: true)
        };

        try
        {
            await ProfileStore.SaveAsync(path, options);
            var loaded = await ProfileStore.LoadAsync(path);

            Assert.Equal(options.CapacityRun, loaded.Options.CapacityRun);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static MailTestOptions CreateOptions() => new(
        From: "tester@example.test",
        Recipients: new[] { "recipient@example.test" },
        SmtpHost: "smtp.example.test",
        Port: 587,
        Security: SmtpSecurity.StartTls,
        UseAuthentication: true,
        Username: "smtp-user",
        Password: "smtp-secret",
        MessageCount: 1,
        IntervalMs: 100,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 1,
        MaxConcurrency: 1,
        Subject: "profile test",
        Body: "profile test",
        DisplayName: "tester",
        RandomTestData: false,
        TestMode: true,
        AllowedDomains: "example.test",
        HtmlBody: false,
        Attachments: Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(),
        IgnoreCertificateErrors: false,
        MaxRetries: 0,
        DryRun: false,
        ProxyPassword: "proxy-secret",
        ClientCertificatePassword: "certificate-secret");
}
