# MailLoadTester payload plugins

Optional `IMailPayloadPlugin` implementations can be placed as trusted `.dll` files in the application's `plugins` directory (next to the executable).

The plugin contract operates on an already-built `MimeMessage`; it is intentionally independent of SMTP connections, proxies, rate limiting, retries, and authorization.

## Contract

- `Order` controls deterministic execution order; lower values run first (then `Name`).
- `Name` is a stable diagnostic identifier.
- `ApplyAsync` may mutate the MIME message and must honor the supplied `CancellationToken`.
- Plugin assemblies must provide a public parameterless constructor.
- Broken assemblies/types are skipped by discovery and reported through the optional logger.

## Lifecycle (I-5)

1. **Discovery** — once per test run via `MailPayloadPluginLoader` (`plugins/*.dll`).
2. **Pipeline** — `MailPayloadPluginPipeline` applies plugins after `BuildMessage`, before bandwidth throttle and SMTP `SendAsync`.
3. **Cancellation** — checked before each plugin; `OperationCanceledException` is not wrapped.
4. **Plugin failure** — non-cancellation exceptions are wrapped as `MailPayloadPluginException` (includes plugin name/order) and fail the **current message** (fail-fast; later plugins are skipped).
5. **Retries** — there is **no** plugin-only retry. Only the runner's existing SMTP `MaxRetries` / AutoRestart may rebuild MIME and re-apply the pipeline for the same logical message index, still under pacing and concurrency gates.
6. **Isolation** — a broken DLL or constructor does not prevent the core tester from starting.

## Security boundary

Plugins are local .NET code and are therefore **trusted code**, not a sandbox. Do not load untrusted DLLs. A plugin cannot be used as a supported mechanism to bypass the tester's authorization, concurrency, rate, retry, or cancellation controls.

No plugin directory is required for normal operation; an absent directory simply means zero optional plugins.
