# Trehwmm/Email-Bomber-SMTP — source-level audit

**Status:** SOURCE-AUDITED · **SECURITY-CRITICAL FINDING**

**Do not build this project on a trusted machine.** The `.vbproj` embeds a Roslyn MSBuild task that decodes and runs native code in-process during build.

## Verified source
- `Email Bomber/Form1.vb`
- `Email Bomber/userinfo.vb`
- `Email Bomber/Email Bomber.vbproj`
- `.github/workflows/dotnet-desktop.yml`
- `README.md`

## SECURITY-CRITICAL — MSBuild in-memory loader

`Email Bomber/Email Bomber.vbproj` contains a `UsingTask` with `RoslynCodeTaskFactory` whose `Execute()` method:

1. `Convert.FromBase64String` on a large embedded blob
2. XOR with three rotating 32-byte keys
3. GZip decompress
4. `VirtualAlloc` → `Marshal.Copy` → `VirtualProtect` → `CreateThread` → `WaitForSingleObject`

This is **not** SMTP functionality. It is an obfuscated native payload runner hooked into the **build** process.

| Item | Decision |
|------|----------|
| Embedded Base64/XOR/GZip native loader | **REJECT — SECURITY CRITICAL** |
| VirtualAlloc / VirtualProtect / CreateThread in MSBuild | **REJECT — SECURITY CRITICAL** |
| Any copy of this build task into load2 | **REJECT** |

**load2 policy:** never accept custom MSBuild/Roslyn tasks that allocate executable memory or decode opaque blobs. Prefer stock SDK builds; review any `UsingTask` / `CodeTaskFactory` in third-party projects before open.

## CI anomaly

`.github/workflows/dotnet-desktop.yml`:

- `on.push` **and** `schedule: cron: "* * * * *"` (every minute)
- writes timestamp into `EMail`
- commits with random/fixed messages
- `ad-m/github-push-action` with **`force: true`**

| Item | Decision |
|------|----------|
| Minute-ly auto-commit + force-push | **REJECT** as practice |
| Branch protection / no force-push on `main` | **HARDEN** for load2 if not already |

## SMTP mechanisms (Form1.vb)

- Up to five SMTP identities as separate `BackgroundWorker` instances
- Each worker: SMTP client, port 587, TLS, credentials, repeated `Send` to finite count
- `CancelAsync` / `CancellationPending` in send loop
- Shared mutable counters `sa1..sa5`; completion tied mainly to worker 1
- Workers 4/5 use wrong `From` (`name3`) — source defect
- Credentials in GUI `ListView` / mutable fields (`userinfo.vb`)
- Hardcoded Gmail endpoint; no retry taxonomy; no central pacing; no ledger

## Transfer decisions

| Mechanism | Decision | Load2 treatment |
|-----------|----------|-----------------|
| Multiple SMTP identities | ADAPT | Session/account pool, not 5× copy-paste |
| Parallel workers | ADOPT | Existing bounded Channel workers |
| Cancellation | ADOPT | `CancellationToken` |
| Finite count | ADAPT | Scenario limits |
| TLS / configurable endpoint | ADAPT | Options, not hardcoded host |
| Central pacing / structured results | ADOPT | SmartPace + ledger / FEAT-REPORT |
| Completion aggregation all workers | ADOPT | `Task.WhenAll` / runner lifecycle |
| Manual 5× worker duplication | REJECT | |
| GUI plaintext passwords | REJECT | |
| Hardcoded Gmail | REJECT | |
| Unrestricted bombing semantics | REJECT | |
| MSBuild native loader | **REJECT — SECURITY CRITICAL** | |
| Force-push minute CI | REJECT / HARDEN branch rules | |

## Conclusion

SMTP path is a weak reference next to load2's MailKit engine. The **primary lasting value of this audit is the security finding**: compromised/abusive build tooling and hostile CI patterns to keep out of load2 supply chain.

**Do not clone-and-build Trehwmm for experimentation.** Static file read only.
