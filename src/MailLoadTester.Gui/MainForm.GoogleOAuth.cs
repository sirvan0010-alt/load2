using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using MailLoadTester;
using MimeKit;

namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private const string GoogleOAuthClientId =
        "717640306481-tt965q9ubsuv2u60iq1ocu9ook3hodv5.apps.googleusercontent.com";

    private readonly HttpClient _googleOAuthHttpClient = new();
    private readonly HttpClient _gmailApiHttpClient = new();
    private GoogleOAuthClient? _googleOAuthClient;
    private GoogleOAuthCredentialStore? _googleOAuthCredentialStore;
    private GoogleOAuthStoredAccount? _googleOAuthStoredAccount;
    private GoogleOAuthTokenSet? _googleOAuthTokens;
    private CancellationTokenSource? _googleOAuthOperationCts;
    private TaskCompletionSource<bool>? _googleOAuthOperationCompletion;

    private CancellationToken BeginGoogleOAuthOperation()
    {
        if (_googleOAuthOperationCts is not null)
            throw new InvalidOperationException("Jiná operace Google OAuth již probíhá.");

        _googleOAuthOperationCts = new CancellationTokenSource();
        _googleOAuthOperationCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        return _googleOAuthOperationCts.Token;
    }

    private void EndGoogleOAuthOperation()
    {
        var cts = _googleOAuthOperationCts;
        _googleOAuthOperationCts = null;
        _googleOAuthOperationCompletion?.TrySetResult(true);
        _googleOAuthOperationCompletion = null;
        cts?.Dispose();
    }

    private void DisposeGoogleOAuthResources()
    {
        _googleOAuthHttpClient.Dispose();
        _gmailApiHttpClient.Dispose();
    }

    private async Task InitializeGoogleOAuthAsync()
    {
        var credentialPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MailLoadTester",
            "google-oauth.dat");
        _googleOAuthCredentialStore = new GoogleOAuthCredentialStore(
            credentialPath, new WindowsDpapiSecretProtector());

        try
        {
            var savedSecret = LoadGoogleOAuthClientSecret();
            _googleOAuthClient = new GoogleOAuthClient(GoogleOAuthClientId, _googleOAuthHttpClient, savedSecret);
            _googleOAuthStoredAccount = await _googleOAuthCredentialStore.LoadAsync();
            if (_googleOAuthStoredAccount is null)
            {
                lblGoogleAccountStatus.Text = "Google účet: nepřipojen";
                btnGoogleSignOut.Enabled = false;
                btnSendGoogleTest.Enabled = false;
                return;
            }

            lblGoogleAccountStatus.Text = "Připojeno: " + _googleOAuthStoredAccount.Email;
            btnGoogleSignOut.Enabled = true;
            btnSendGoogleTest.Enabled = true;
            if (string.IsNullOrWhiteSpace(txtFrom.Text))
                txtFrom.Text = _googleOAuthStoredAccount.Email;
        }
        catch (Exception ex)
        {
            lblGoogleAccountStatus.Text = "Google účet: uložené přihlášení nelze načíst";
            btnGoogleSignOut.Enabled = true;
            btnSendGoogleTest.Enabled = false;
            toolTip.SetToolTip(lblGoogleAccountStatus, ex.Message);
        }
    }

    private GoogleOAuthClient EnsureGoogleOAuthClientWithCredentials()
    {
        var secret = LoadGoogleOAuthClientSecret();
        if (string.IsNullOrWhiteSpace(secret))
        {
            secret = PromptForGoogleOAuthClientSecret(this);
            if (string.IsNullOrWhiteSpace(secret))
                throw new OperationCanceledException("Zadání Google OAuth Client Secret bylo zrušeno.");
            SaveGoogleOAuthClientSecret(secret);
        }

        return _googleOAuthClient = new GoogleOAuthClient(
            GoogleOAuthClientId, _googleOAuthHttpClient, secret);
    }

    private void OnConfigureGoogleOAuthClientSecret()
    {
        if (_isRunning || _testConnectionCts is not null || _googleOAuthOperationCts is not null)
            return;

        try
        {
            var secret = PromptForGoogleOAuthClientSecret(
                this,
                "Vlož Client Secret pro OAuth klienta MailLoadTester Desktop. Pokud Google předchozí hodnotu odmítl, zadej nově vytvořený secret patřící ke stejnému Client ID. Nejde o heslo k tvému Gmailu. U desktopových aplikací není Client Secret skutečným tajemstvím; tato verze jej ale vyžaduje při komunikaci s token endpointem a uloží jej lokálně zašifrovaně pro tento účet Windows.");
            if (string.IsNullOrWhiteSpace(secret))
                return;

            SaveGoogleOAuthClientSecret(secret);
            _googleOAuthClient = new GoogleOAuthClient(
                GoogleOAuthClientId, _googleOAuthHttpClient, secret);
            MessageBox.Show(
                "Google OAuth Client Secret byl uložen. Při dalším přihlášení nebo odeslání se použije tato hodnota.",
                "Google OAuth", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowErrorWithCopy(
                "Google OAuth Client Secret se nepodařilo uložit: " + ex.Message,
                "Nastavení Google OAuth", ex.ToString(), GuiErrorCodes.Validation);
        }
    }

    private static string SecretFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MailLoadTester", "google-oauth-client-secret.dat");

    private static string? LoadGoogleOAuthClientSecret()
    {
        if (!File.Exists(SecretFilePath))
            return null;

        var protector = new WindowsDpapiSecretProtector();
        var encrypted = File.ReadAllBytes(SecretFilePath);
        byte[]? clear = null;
        try
        {
            clear = protector.Unprotect(encrypted);
            return Encoding.UTF8.GetString(clear);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
            if (clear is not null) CryptographicOperations.ZeroMemory(clear);
        }
    }

    private static void SaveGoogleOAuthClientSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Google OAuth Client Secret nesmí být prázdný.", nameof(secret));

        var directory = Path.GetDirectoryName(SecretFilePath)!;
        Directory.CreateDirectory(directory);
        var tempPath = SecretFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var protector = new WindowsDpapiSecretProtector();
        var clear = Encoding.UTF8.GetBytes(secret.Trim());
        byte[]? encrypted = null;
        try
        {
            encrypted = protector.Protect(clear);
            File.WriteAllBytes(tempPath, encrypted);
            File.Move(tempPath, SecretFilePath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clear);
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup; the target file remains the last committed value.
            }
        }
    }

    private static string? PromptForGoogleOAuthClientSecret(
        IWin32Window owner,
        string? promptText = null)
    {
        using var dialog = new Form
        {
            Text = "Nastavení Google OAuth Client Secret",
            Width = 560,
            Height = 205,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };
        var label = new Label
        {
            Left = 16, Top = 16, Width = 510, Height = 54,
            Text = promptText ?? "Vlož Client Secret pro OAuth klienta MailLoadTester Desktop. Nejde o heslo k tvému Gmailu. U desktopových aplikací není Client Secret skutečným tajemstvím; MailLoadTester jej přesto vyžaduje současnou konfigurací a uloží jej lokálně zašifrovaně pro tento účet Windows. Pokud jej Google odmítne, můžeš jej změnit tlačítkem Nastavit / změnit Client Secret."
        };
        var input = new TextBox
        {
            Left = 16, Top = 78, Width = 510,
            UseSystemPasswordChar = true
        };
        var ok = new Button
        {
            Text = "Uložit a pokračovat", Left = 300, Top = 118, Width = 145,
            DialogResult = DialogResult.OK
        };
        var cancel = new Button
        {
            Text = "Zrušit", Left = 455, Top = 118, Width = 70,
            DialogResult = DialogResult.Cancel
        };
        dialog.Controls.AddRange([label, input, ok, cancel]);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(owner) == DialogResult.OK
            ? input.Text.Trim()
            : null;
    }

    private async Task OnGoogleSignInAsync()
    {
        if (_isRunning || _testConnectionCts is not null || _googleOAuthOperationCts is not null)
            return;

        var operationToken = BeginGoogleOAuthOperation();
        btnGoogleSignIn.Enabled = false;
        btnGoogleSignOut.Enabled = false;
        btnSendGoogleTest.Enabled = false;
        lblGoogleAccountStatus.Text = "Čekám na přihlášení v prohlížeči…";
        try
        {
            _googleOAuthClient = EnsureGoogleOAuthClientWithCredentials();
            var flow = new GoogleOAuthLoopbackFlow();
            var result = await flow.AuthorizeAsync(
                _googleOAuthClient,
                (uri, _) =>
                {
                    Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
                    return Task.CompletedTask;
                },
                TimeSpan.FromMinutes(3),
                operationToken);

            var stored = new GoogleOAuthStoredAccount(
                result.User.Subject, result.User.Email, result.Tokens.RefreshToken);
            await (_googleOAuthCredentialStore
                ?? throw new InvalidOperationException("Úložiště Google OAuth není připravené."))
                .SaveAsync(stored, operationToken);

            _googleOAuthStoredAccount = stored;
            _googleOAuthTokens = result.Tokens;
            lblGoogleAccountStatus.Text = "Připojeno: " + stored.Email;
            btnGoogleSignOut.Enabled = true;
            btnSendGoogleTest.Enabled = true;
            if (string.IsNullOrWhiteSpace(txtFrom.Text))
                txtFrom.Text = stored.Email;

            MessageBox.Show(
                "Google účet byl úspěšně připojen. Přihlašovací údaje jsou zašifrované pro aktuální účet Windows.",
                "Google účet připojen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            lblGoogleAccountStatus.Text = "Google účet: přihlášení zrušeno";
        }
        catch (TimeoutException ex)
        {
            lblGoogleAccountStatus.Text = "Google účet: přihlášení vypršelo";
            MessageBox.Show(ex.Message, "Přihlášení Google", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (GoogleOAuthException ex) when (
            string.Equals(ex.OAuthError, "invalid_client", StringComparison.Ordinal))
        {
            lblGoogleAccountStatus.Text = "Google OAuth: Client Secret byl odmítnut";
            var replacement = PromptForGoogleOAuthClientSecret(
                this,
                "Google odmítl uložený Client Secret (invalid_client). Zadej správný secret patřící ke stejnému OAuth Client ID. Po uložení spusť přihlášení znovu.");
            if (!string.IsNullOrWhiteSpace(replacement))
            {
                SaveGoogleOAuthClientSecret(replacement);
                _googleOAuthClient = new GoogleOAuthClient(
                    GoogleOAuthClientId, _googleOAuthHttpClient, replacement);
                MessageBox.Show(
                    "Nový Client Secret byl uložen. Přihlašovací pokus se již nedokončí; klikni znovu na Přihlásit přes Google.",
                    "Google OAuth", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                ShowErrorWithCopy(
                    "Google odmítl Client Secret. Můžeš jej opravit tlačítkem Nastavit / změnit Client Secret. " + ex.Message,
                    "Google OAuth – neplatný Client Secret", ex.ToString(), GuiErrorCodes.Validation);
            }
        }
        catch (Exception ex)
        {
            lblGoogleAccountStatus.Text = "Google účet: přihlášení se nezdařilo";
            ShowErrorWithCopy(
                "Přihlášení přes Google se nepodařilo: " + ex.Message,
                "Google OAuth", ex.ToString(), GuiErrorCodes.Validation);
        }
        finally
        {
            EndGoogleOAuthOperation();
            btnGoogleSignIn.Enabled = true;
            btnGoogleSignOut.Enabled = _googleOAuthStoredAccount is not null;
            btnSendGoogleTest.Enabled = _googleOAuthStoredAccount is not null;
        }
    }

    private async Task OnGoogleSignOutAsync()
    {
        if (_isRunning || _testConnectionCts is not null || _googleOAuthOperationCts is not null)
            return;

        var operationToken = BeginGoogleOAuthOperation();
        btnGoogleSignIn.Enabled = false;
        btnGoogleSignOut.Enabled = false;
        btnSendGoogleTest.Enabled = false;
        var account = _googleOAuthStoredAccount;
        var revokeWarning = string.Empty;
        try
        {
            if (account is not null && _googleOAuthClient is not null)
            {
                try
                {
                    await _googleOAuthClient.RevokeAsync(account.RefreshToken, operationToken);
                }
                catch (Exception ex)
                {
                    revokeWarning = "\nGoogle se nepodařilo potvrdit vzdálené odvolání: " + ex.Message;
                }
            }

            await (_googleOAuthCredentialStore
                ?? throw new InvalidOperationException("Úložiště Google OAuth není připravené."))
                .DeleteAsync(operationToken);

            _googleOAuthStoredAccount = null;
            _googleOAuthTokens = null;
            lblGoogleAccountStatus.Text = "Google účet: nepřipojen";
            btnGoogleSignOut.Enabled = false;
            btnSendGoogleTest.Enabled = false;

            MessageBox.Show(
                "Google účet byl odpojen a místní obnovovací token odstraněn." + revokeWarning,
                "Google účet", MessageBoxButtons.OK,
                string.IsNullOrEmpty(revokeWarning) ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            lblGoogleAccountStatus.Text = "Google účet: odpojení zrušeno";
        }
        catch (Exception ex)
        {
            ShowErrorWithCopy(
                "Google účet se nepodařilo odpojit: " + ex.Message,
                "Odpojení Google účtu", ex.ToString(), GuiErrorCodes.Validation);
        }
        finally
        {
            EndGoogleOAuthOperation();
            btnGoogleSignIn.Enabled = true;
            btnGoogleSignOut.Enabled = _googleOAuthStoredAccount is not null;
            btnSendGoogleTest.Enabled = _googleOAuthStoredAccount is not null;
        }
    }

    private async Task OnSendGoogleTestMessageAsync()
    {
        if (_isRunning || _testConnectionCts is not null || _googleOAuthOperationCts is not null)
            return;

        var account = _googleOAuthStoredAccount;
        if (account is null)
        {
            MessageBox.Show("Nejdříve klikni na Přihlásit přes Google.", "Gmail API",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        MimeMessage message;
        try
        {
            message = BuildGoogleApiMessage(account);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Zprávu nelze připravit: " + ex.Message, "Gmail API",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var recipients = string.Join("\n", new[]
        {
            message.To.Count > 0 ? "Komu: " + message.To : null,
            message.Cc.Count > 0 ? "Kopie: " + message.Cc : null,
            message.Bcc.Count > 0 ? "Skrytá kopie: " + message.Bcc : null
        }.Where(line => line is not null));

        var confirmation = MessageBox.Show(
            "Bude odeslána JEDNA skutečná zpráva přes Gmail API.\n\n" +
            "Přihlášený účet: " + account.Email + "\n" +
            recipients + "\nPředmět: " + message.Subject + "\n\n" +
            "Toto není SMTP test a nebude započítán do SMTP statistik. Pokračovat?",
            "Potvrdit odeslání přes Gmail API",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
            return;

        var operationToken = BeginGoogleOAuthOperation();
        btnSendGoogleTest.Enabled = false;
        btnGoogleSignIn.Enabled = false;
        btnGoogleSignOut.Enabled = false;
        lblGoogleAccountStatus.Text = "Odesílám přes Gmail API…";
        try
        {
            EnsureGoogleOAuthClientWithCredentials();
            var tokens = await GetCurrentGoogleTokensAsync(account, operationToken);
            var sender = new GoogleGmailApiClient(_gmailApiHttpClient);
            var result = await sender.SendAsync(message, _googleOAuthClient!, tokens, operationToken);

            _googleOAuthTokens = result.Tokens;
            var updatedAccount = account with { RefreshToken = result.Tokens.RefreshToken };
            await _googleOAuthCredentialStore!.SaveAsync(updatedAccount, operationToken);
            _googleOAuthStoredAccount = updatedAccount;

            lblGoogleAccountStatus.Text = "Připojeno: " + updatedAccount.Email;
            MessageBox.Show(
                "Gmail API přijalo zprávu k odeslání.\n\nID zprávy: " + result.MessageId +
                "\n\nPřijetí API nepotvrzuje doručení do schránky příjemce.",
                "Zpráva předána Gmailu", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            lblGoogleAccountStatus.Text = "Připojeno: " + account.Email;
            MessageBox.Show(
                "Odesílání bylo zrušeno. Pokud už požadavek dorazil Googlu, nelze vyloučit, že zprávu přijal. Před opakováním zkontroluj odeslanou poštu.",
                "Gmail API – zrušeno", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (GoogleOAuthException ex) when (
            string.Equals(ex.OAuthError, "invalid_client", StringComparison.Ordinal))
        {
            lblGoogleAccountStatus.Text = "Google OAuth: Client Secret byl odmítnut";
            var replacement = PromptForGoogleOAuthClientSecret(
                this,
                "Google odmítl uložený Client Secret (invalid_client). Zadej správný secret patřící ke stejnému OAuth Client ID. Potom zkus odeslání znovu.");
            if (!string.IsNullOrWhiteSpace(replacement))
            {
                SaveGoogleOAuthClientSecret(replacement);
                _googleOAuthClient = new GoogleOAuthClient(
                    GoogleOAuthClientId, _googleOAuthHttpClient, replacement);
                MessageBox.Show(
                    "Nový Client Secret byl uložen. Zpráva nebyla odeslána. Klikni znovu na Odeslat 1 zprávu přes Gmail API.",
                    "Google OAuth", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                ShowErrorWithCopy(
                    "Google odmítl Client Secret. Můžeš jej opravit tlačítkem Nastavit / změnit Client Secret. " + ex.Message,
                    "Gmail API – neplatný Client Secret", ex.ToString(), GuiErrorCodes.Validation);
            }
        }
        catch (GoogleOAuthException ex)
        {
            lblGoogleAccountStatus.Text = "Google účet: je nutné znovu se přihlásit";
            ShowErrorWithCopy(
                "Google odmítl obnovit přístup. Odpoj účet a přihlas se znovu. " + ex.Message,
                "Gmail API – přihlášení", ex.ToString(), GuiErrorCodes.Validation);
        }
        catch (GoogleGmailApiException ex)
        {
            lblGoogleAccountStatus.Text = "Připojeno: " + account.Email;
            ShowErrorWithCopy(
                "Gmail API zprávu nepřijalo: " + ex.Message,
                "Gmail API – odeslání", ex.ToString(), GuiErrorCodes.Validation);
        }
        catch (Exception ex)
        {
            lblGoogleAccountStatus.Text = "Připojeno: " + account.Email;
            ShowErrorWithCopy(
                "Odeslání přes Gmail API se nepodařilo: " + ex.Message,
                "Gmail API – odeslání", ex.ToString(), GuiErrorCodes.Validation);
        }
        finally
        {
            EndGoogleOAuthOperation();
            btnGoogleSignIn.Enabled = true;
            btnGoogleSignOut.Enabled = _googleOAuthStoredAccount is not null;
            btnSendGoogleTest.Enabled = _googleOAuthStoredAccount is not null;
        }
    }

    private async Task<GoogleOAuthTokenSet> GetCurrentGoogleTokensAsync(
        GoogleOAuthStoredAccount account,
        CancellationToken cancellationToken)
    {
        if (_googleOAuthTokens is { } cached
            && cached.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return cached;

        var oauth = _googleOAuthClient
            ?? throw new InvalidOperationException("Google OAuth klient není inicializovaný.");
        var previous = new GoogleOAuthTokenSet(
            string.Empty, account.RefreshToken, DateTimeOffset.MinValue,
            "Bearer", GoogleOAuthClient.GmailSendScope);
        var refreshed = await oauth.RefreshAsync(previous, cancellationToken);
        var identity = await oauth.GetUserAsync(refreshed.AccessToken, cancellationToken);

        if (!string.Equals(identity.Subject, account.Subject, StringComparison.Ordinal)
            || !string.Equals(identity.Email, account.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Google vrátil jiný účet, než který je uložený. Odpoj účet a přihlas se znovu.");

        _googleOAuthTokens = refreshed;
        var updated = account with { RefreshToken = refreshed.RefreshToken };
        await _googleOAuthCredentialStore!.SaveAsync(updated, cancellationToken);
        _googleOAuthStoredAccount = updated;
        return refreshed;
    }

    private MimeMessage BuildGoogleApiMessage(GoogleOAuthStoredAccount account)
    {
        var from = txtFrom.Text.Trim();
        if (!string.IsNullOrWhiteSpace(from)
            && !from.Equals(account.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Pole Od musí odpovídat přihlášenému Gmail účtu (" + account.Email + ").");

        var message = new MimeMessage();
        var displayName = txtDisplayName.Text.Trim();
        message.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(displayName) ? account.Email : displayName,
            account.Email));

        AddGoogleRecipients(message.To, txtTo.Text, "Komu");
        AddGoogleRecipients(message.Cc, txtCc.Text, "Kopie");
        AddGoogleRecipients(message.Bcc, txtBcc.Text, "Skrytá kopie");
        if (message.To.Count + message.Cc.Count + message.Bcc.Count == 0)
            throw new InvalidOperationException("Zadej alespoň jednoho příjemce na kartě E-mail.");

        message.Subject = txtSubject.Text ?? string.Empty;
        var body = new BodyBuilder();
        if (chkHtmlBody.Checked)
            body.HtmlBody = txtBody.Text;
        else
            body.TextBody = txtBody.Text;

        foreach (var attachment in lstAttachments.Items.Cast<string>())
        {
            if (!File.Exists(attachment))
                throw new FileNotFoundException("Příloha nebyla nalezena.", attachment);
            body.Attachments.Add(attachment);
        }

        message.Body = body.ToMessageBody();
        return message;
    }

    private static void AddGoogleRecipients(
        InternetAddressList target,
        string input,
        string fieldName)
    {
        foreach (var address in input.Split(
            new[] { ',', ';', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                target.Add(MailboxAddress.Parse(address));
            }
            catch (ParseException ex)
            {
                throw new InvalidOperationException(
                    "Neplatná e-mailová adresa v poli " + fieldName + ": " + address, ex);
            }
        }
    }
}
