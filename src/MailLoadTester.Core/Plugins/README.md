# MailLoadTester payload plugins

Optional `IMailPayloadPlugin` implementations can be placed as trusted `.dll` files in the application's `plugins` directory.

The plugin contract operates on an already-built `MimeMessage`; it is intentionally independent of SMTP connections, proxies, rate limiting, retries, and authorization.

## Contract

- `Order` controls deterministic execution order; lower values run first.
- `Name` is a stable diagnostic identifier.
- `ApplyAsync` may mutate the MIME message and must honor the supplied `CancellationToken`.
- Plugin assemblies must provide a public parameterless constructor.
- Broken assemblies/types are skipped by discovery and reported through the optional logger.

## Security boundary

Plugins are local .NET code and are therefore **trusted code**, not a sandbox. Do not load untrusted DLLs. A plugin cannot be used as a supported mechanism to bypass the tester's authorization, concurrency, rate, retry, or cancellation controls.

No plugin directory is required for normal operation; an absent directory simply means zero optional plugins.
