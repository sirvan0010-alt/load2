# MailLoadTester — payload plugins

## Purpose

`IMailPayloadPlugin` is an optional extension point for modifying an already-built `MimeMessage` before SMTP submission. The plugin API is transport-agnostic: plugins do not receive the SMTP client, connection pool, proxy, rate limiter, concurrency limiter, or authorization state.

## Package layout

Normal installations do not require a `plugins` directory. When optional plugins are used, place trusted plugin assemblies here:

```text
MailLoadTester.exe
plugins/
  MyPayloadPlugin.dll
```

The application discovers `*.dll` files directly inside `plugins/` at startup of each test run. Subdirectories are not scanned.

## Plugin contract

A plugin assembly must contain a concrete public class implementing `IMailPayloadPlugin` with a public parameterless constructor.

Execution order is deterministic:

1. lower `Order` first;
2. then `Name` using ordinal comparison.

`ApplyAsync` receives a `MimeMessage` and `MailPayloadPluginContext`. The supplied `CancellationToken` must be honored.

## Runtime lifecycle

```text
BuildMessage()
    -> payload plugin pipeline
    -> bandwidth limiting
    -> global SMTP pacing
    -> SmtpClient.SendAsync()
```

Plugins are discovered once per test run and applied after MIME construction and before the message reaches SMTP `SendAsync`.

A non-cancellation plugin exception is reported as `MailPayloadPluginException` and fails the current message. Later plugins are not executed for that message. There is no plugin-specific retry mechanism; existing SMTP retry/AutoRestart behavior remains the only retry path and remains subject to the normal pacing, concurrency and authorization gates.

Cancellation is not wrapped: `OperationCanceledException` propagates normally.

## Installation and release

- No plugin DLL is required for the base application.
- A missing `plugins` directory is a valid zero-plugin configuration.
- Plugin DLLs are optional deployment artifacts and should be distributed alongside the published executable under `plugins/`.
- The installer does not need to create an empty `plugins` directory for normal operation.
- Only trusted plugin assemblies should be deployed. Plugins are normal .NET code and are **not sandboxed**.
- Plugin loading is intentionally limited to the application's `plugins` directory; arbitrary paths are not accepted by the default discovery path.

## Safety boundary

Plugins are an extension mechanism, not a way to bypass tester controls. They must not be used to disable authorization, pacing, concurrency, cancellation, retry limits, TLS/security checks, or other safety gates, and the project remains an authorized, bounded mail-load tester rather than an uncontrolled mailbombing, flooding, spam, DoS, or DDoS tool.

For the API-level contract and lifecycle details, see `src/MailLoadTester.Core/Plugins/README.md`.
