# External Repository Transfer Audit — `load2`

**Status:** AUDIT / ADOPTION BACKLOG
**Authority:** `sirvan0010-alt/load2` / `main`
**Purpose:** Preserve useful engineering ideas found in external "bomber", scanner, automation and load-generation repositories so a future AI can understand exactly why each repository is retained, what was learned from it, and what should be implemented in `load2`.

> This document is a research/adoption plan. External repositories are **not** sources of truth for `load2`. The only source of truth for the implementation is `sirvan0010-alt/load2/main`.

## 1. Important interpretation: offensive functions are part of the audit

The earlier statement "do not take offensive functions" was too broad for this project.

We **do want to study offensive implementations**, including flooding, multi-provider dispatch, concurrency, endpoint rotation, repeated delivery, failure handling and attack/scenario modes, because they can reveal useful transport, scheduling, orchestration and testing techniques.

What changes is the implementation boundary:

- We may adopt the **engineering mechanism** when it improves `load2`.
- We may implement an **authorized/lab equivalent** of an offensive capability when it is useful for security testing.
- We must not turn `load2` into an unrestricted public-target flooding tool or add mechanisms whose primary purpose is bypassing provider controls or causing uncontrolled harm.
- The existing `--unauthorized` gate, DryRun/TestMode rules, bounded concurrency, pacing, cancellation, allowlists and explicit target validation remain mandatory safety boundaries.

Therefore, "offensive" does **not** mean "ignore the repository". It means "audit the technique, extract the reusable architecture, and implement only the controlled capability that belongs in an authorized testing framework".

## 2. Adoption model for every external repository

For each repository we keep four separate decisions:

1. **KEEP REFERENCE** — repository remains recorded because it contains useful ideas.
2. **ADOPT ARCHITECTURE** — copy the design principle into `load2`, not the original implementation language.
3. **ADOPT CONTROLLED FUNCTION** — implement a bounded/authorized equivalent in `load2`.
4. **DO NOT IMPORT** — do not copy credentials, public target lists, bypasses, stealth logic, destructive automation or unrelated malware-like behavior.

No external code is copied into `load2` automatically. Each implementation must first be mapped to existing interfaces and tested against `main`.

---

## 3. Repository audit table

| Repository | Why it is stored | Concrete ideas to transfer to `load2` | Adoption | Audit depth |
|---|---|---|---|---|
| https://github.com/un1cum/Beast_Bomber | Multi-channel offensive dispatcher; useful for understanding a single orchestrator driving different transports. | Transport/provider abstraction; separate target/channel adapters; common progress/error handling; configurable execution modes; proxy abstraction as a transport concern; multi-language/UX separation. | **ADOPT architecture + controlled function** | README + repository metadata reviewed. README documents SMS, email, Discord, Telegram, DDoS and proxy-backed operation. |
| https://github.com/bhattsameer/Bombers | Collection of many bomber implementations; useful as a catalog of patterns and failure modes. | Provider registry; capability matrix; per-provider health/status; dead-provider exclusion; provider-specific adapters; result classification; modular service definitions. | **ADOPT architecture** | README reviewed. Repository is archived and contains a broad collection; individual child projects need separate audit before implementation. |
| https://github.com/tr0uble-mAker/POC-bomber | High-concurrency security scanner with pluggable POC files and verify/attack separation. | Plugin registry; `Verify` vs `Execute` separation; per-plugin metadata; timeout isolation; bounded worker pool; batch targets; structured vulnerability/result reports; plugin discovery. | **ADOPT architecture + controlled function** | README reviewed. It explicitly documents custom POCs, verify/attack functions, concurrency, batch scanning, reports and optional DNS-log based detection. |
| https://github.com/yyasha/smsbomb | SMS bombing reference; useful for provider fan-out and external-service failure handling. | Provider adapter abstraction; per-provider capability/status; retry classification; provider health metrics; strict global and per-provider limits. | **ADOPT controlled function** | Repository retained as requested; detailed source audit required before code adoption. |
| https://github.com/devops-kung-fu/bomber | Security/SBOM scanner rather than a mail bomber; architecturally one of the most useful references. | Provider → enrichment → filter → renderer → exit-code pipeline; CycloneDX/SPDX/Syft input concepts; multiple vulnerability providers; EPSS-style enrichment; folder/STDIN input; JSON/Markdown/HTML output; CI-friendly exit codes. | **ADOPT architecture** | README/release information reviewed. This repository is explicitly useful despite unrelated domain. |
| https://github.com/ncorbuk/Python---Email-Bomber | Simple educational SMTP/email automation example. | Keep as historical reference for SMTP abstraction and configuration separation; use only to compare old SMTP assumptions with current MailKit/MimeKit design. | **REFERENCE ONLY** | README reviewed. It is a tutorial-era project and relies on old "less secure apps" assumptions; do not copy that authentication model. |
| https://github.com/hackerxphantom/X_BOMB | Offensive multi-service automation reference. | Common scenario model; target/channel validation; execution status aggregation; modular service handlers if source confirms clean separation. | **ADOPT only after source audit** | Repository retained from requested list; implementation-level audit pending. |
| https://github.com/anubhavanonymous/XLR8_BOMBER | Fast SMS/call/WhatsApp automation; useful for studying high-throughput dispatch and platform constraints. | Fast startup; dependency checks; capability detection; service health checks; execution statistics; explicit platform capability reporting. | **ADOPT architecture** | README reviewed. Project is marked closed in README; no direct offensive mechanics are imported. |
| https://github.com/juzeon/fast-mail-bomber | Strong reference for provider/node discovery, deduplication, dead-provider tracking and parallel dispatch. | Provider discovery abstraction; node discovery; deduplication; dead-provider quarantine; health refresh; provider/node inventories; controlled parallel execution; exception aggregation. | **ADOPT architecture + controlled function** | README reviewed. It documents provider/node discovery, Shodan/ZoomEye/local imports, duplicate removal, dead-provider tracking, multithreading and exception handling. Its target-flooding workflow is not imported. |
| https://github.com/mohinparamasivam/Email-Bomber | Additional SMTP/email bomber implementation for comparative analysis. | Compare SMTP connection lifecycle, message generation and retry behavior; extract only patterns that outperform current `load2` implementation. | **REFERENCE / compare** | Repository retained from requested list; source-level audit pending. |
| https://github.com/OpticHalberdier/sms-bomber | SMS provider/fan-out reference. | Provider adapter pattern; provider failure classification; controlled fan-out; per-provider rate limits. | **ADOPT controlled function** | Repository retained from requested list; source-level audit pending. |
| https://github.com/brolyklade7/Mail-Bomber-Full-Version | Mail automation reference for comparing SMTP/provider orchestration patterns. | Compare queueing, retries, provider selection, configuration and reporting against current `load2`; adopt only superior architectural patterns. | **REFERENCE / compare** | Repository retained from requested list; source-level audit pending. |
| https://github.com/niushamjd/bomberman-rl-pinkbombers | Bomberman/RL project, unrelated to email; useful for reproducible automated experiments. | Run IDs; replayable scenarios; experiment artifacts; deterministic configuration; results directories; automated training/test runs; machine-readable metrics. | **ADOPT architecture** | Repository family reviewed as an experiment/reproducibility reference. |
| https://github.com/Trehwmm/Email-Bomber-SMTP | Direct SMTP bomber reference, relevant to current transport layer. | Compare SMTP session reuse, connection lifecycle, TLS handling, authentication errors, retry classification and throughput measurement. | **REFERENCE / compare** | Repository retained from requested list; source-level audit pending. |
| https://github.com/noluckkid/wade-miller-bombers | Requested bomber reference; retain for comparative audit. | Search for reusable orchestration, provider abstraction, result aggregation and configuration patterns before any implementation decision. | **REFERENCE / audit later** | Repository retained from requested list; source-level audit pending. |

---

## 4. Functions we specifically want to add to `load2`

These are the concrete capabilities to investigate and implement where they fit the existing architecture.

### 4.1 Provider / transport abstraction

Create a common transport concept around the existing SMTP and Direct-MX implementations instead of putting network logic into message generation.

Conceptual interface:

```csharp
public interface ITransportProvider
{
    string Id { get; }
    IReadOnlyCollection<string> Capabilities { get; }
    bool CanHandle(TestContext context);
    Task<TestResult> ExecuteAsync(
        TestContext context,
        CancellationToken cancellationToken);
}
```

This is a **design target**, not an instruction to create this exact interface without first checking the existing `load2` types.

Candidate capabilities:

- `smtp`
- `persistent-session`
- `tls`
- `auth`
- `proxy`
- `direct-mx`
- `dns`
- `spf`
- `dkim`
- `dmarc`
- `open-relay-check`
- `diagnostics`

### 4.2 Provider registry and health state

Transfer the useful part of the bomber/provider model:

```text
provider discovery
    ↓
provider normalization / deduplication
    ↓
health check
    ↓
healthy provider registry
    ↓
execution
    ↓
failure classification
    ↓
quarantine / cooldown
    ↓
health recovery
```

For `load2`, this must be bounded and target-authorized. It is not a mechanism for discovering arbitrary public infrastructure to flood.

### 4.3 Dead-provider / endpoint quarantine

`load2` already has proxy blocking concepts. Extend the same idea to transport endpoints:

- temporary quarantine after repeated failures;
- exponential or configurable cooldown;
- success-based recovery;
- reason codes;
- metrics for healthy/blocked/dead endpoints;
- cancellation-safe state transitions.

This directly complements the existing `ProxyRotator` and SMTP circuit-breaker logic.

### 4.4 Provider/node deduplication

Adopt the strong idea from provider/node based tools:

- canonicalize endpoint URI;
- normalize host casing and default ports;
- remove duplicate endpoints before execution;
- keep stable endpoint IDs;
- persist optional health state between runs when explicitly enabled.

### 4.5 Scenario engine

Borrow the useful concept behind attack tools without making the engine an unrestricted attack launcher.

A scenario should describe:

```text
target scope
transport
payload plugin
message count
concurrency
rate limit
retry policy
TLS policy
DNS checks
stop conditions
cancellation policy
output format
```

The same scenario can then run against a local lab, test SMTP server, authorized test recipient or explicitly authorized MX.

### 4.6 Verify / Execute separation

From POC-style tools, preserve a hard conceptual distinction:

```text
VERIFY
  ↓
collect evidence / classify capability
  ↓
EXECUTE
  ↓
perform explicitly authorized test action
```

For `load2` this can become:

```text
SMTP capability check
→ TLS/auth/relay/delivery diagnostics
→ evidence
→ optional authorized delivery/load scenario
```

The separation must make it impossible for a passive diagnostic operation to silently become an active flooding operation.

### 4.7 Controlled high-concurrency execution

The useful part of bomber implementations is not "more threads". It is efficient scheduling.

`load2` should keep:

- bounded `Channel<T>` queues;
- fixed worker count;
- `SemaphoreSlim` protection;
- adaptive concurrency;
- global actual-SEND pacing;
- per-recipient limits;
- cancellation propagation;
- retry admission through the same pacing gate;
- no task-per-message explosion.

This is already part of the current repair work and should be treated as a core invariant.

### 4.8 Multi-target / multi-domain scenarios — safely bounded

The external projects demonstrate that one run can cover many targets/providers.

`load2` should support this only with explicit scope validation:

```text
scenario
  → authorized target set
  → validation
  → target-specific transport selection
  → bounded execution
  → per-target result
  → global result
```

Direct MX must continue to enforce its current domain constraints where required by the implementation.

### 4.9 Failure classification and retry policy

Adopt a richer classification model:

```text
SUCCESS
TRANSIENT_NETWORK
TRANSIENT_SMTP
RATE_LIMITED
AUTH_FAILURE
TLS_FAILURE
POLICY_REJECTED
INVALID_TARGET
PERMANENT_FAILURE
CANCELLED
```

Retries should be driven by classification, not simply by "exception happened".

Every retry must pass through the same adaptive concurrency, pool admission and actual-SEND pacing path already established in `load2`.

### 4.10 Structured result and evidence model

From scanner and bomber reporting systems:

```text
RunResult
 ├─ scenario
 ├─ target
 ├─ transport
 ├─ attempts
 ├─ latency
 ├─ SMTP response
 ├─ TLS information
 ├─ DNS information
 ├─ classification
 ├─ evidence
 └─ timestamps
```

This should support Console, JSON, Markdown and HTML renderers without coupling rendering to network code.

### 4.11 Explicit exit codes

Introduce a documented exit-code taxonomy after auditing existing `load2` CLI behavior. Candidate categories:

- `0` success
- `1` runtime error
- `2` configuration/validation error
- `3` connectivity failure
- `4` authentication/TLS failure
- `5` security/scope rejection
- `6` partial failure
- `7` cancellation

**Do not implement these exact numbers until existing CLI behavior is audited.** Avoid breaking compatibility.

### 4.12 Reproducible run artifacts

Adopt the experiment/replay idea:

```text
runs/
  <timestamp-or-run-id>/
    run.json
    summary.json
    timing.json
    failures.json
    smtp.json
    dns.json
    tls.json
```

Sensitive data must be redacted. Credentials and authentication secrets must never be persisted.

### 4.13 DNS and email-security enrichment

The external-tool architecture strongly supports a separate enrichment layer:

```text
transport result
    ↓
DNS enrichment
    ├─ MX
    ├─ SPF
    ├─ DKIM
    └─ DMARC
    ↓
TLS enrichment
    ├─ protocol
    ├─ certificate
    └─ negotiated security
    ↓
deliverability / SMTP evidence
```

This belongs outside the SMTP message generator and should be independently testable.

---

## 5. Offensive capabilities: what we actually mean by "adopt"

The project can study offensive mechanics, but implementation must convert them into controlled security-testing capabilities.

### Useful to adopt in controlled form

- repeated-send **test scenarios** with explicit limits;
- configurable concurrency stress tests;
- provider failover simulation;
- endpoint health/quarantine;
- transport fan-out inside an explicit allowlist;
- retry storms **simulated or bounded** to test the scheduler;
- malformed-message and protocol robustness tests against owned/lab infrastructure;
- SMTP/TLS failure injection;
- DNS misconfiguration tests against controlled domains;
- open-relay verification against explicitly authorized servers;
- rate-limit response detection and reporting;
- cancellation and recovery stress testing;
- multi-transport orchestration;
- deterministic scenario replay.

### Do not import as unrestricted production functionality

- bypassing provider rate limits;
- CAPTCHA/OTP bypass;
- credential/token theft or account harvesting;
- stealth/spoofing intended to conceal abuse;
- public API lists whose purpose is flooding third parties;
- automatic discovery of arbitrary public targets for abuse;
- unrestricted DoS/DDoS launchers;
- automatic escalation from detection into compromise;
- hardcoded credentials or token collections;
- purchased/stolen account inventories;
- mechanisms whose primary purpose is evading abuse controls.

The important distinction is **capability vs. abuse path**. For example, a rate-limit detector is useful; a rate-limit bypasser is not required for `load2`.

---

## 6. What should NOT be copied from external repositories

Never copy code merely because it is faster or simpler.

Do not copy:

- Python/PHP-specific architecture into C#;
- obsolete SMTP authentication assumptions;
- hardcoded provider credentials;
- public service inventories;
- arbitrary proxy scraping;
- uncontrolled task/thread creation;
- global mutable state without synchronization;
- retry loops without cancellation;
- network operations hidden inside payload generation;
- secrets stored in configuration files or logs;
- destructive `attack` functions from security PoC repositories.

All adopted behavior must fit the existing `load2` architecture and pass the current CI/security/test gates.

---

## 7. Implementation order after the current repair work

When the current bug/security/CI repair sequence is complete, audit and implement in this order:

1. **Transport/provider abstraction audit** against existing SMTP + Direct MX code.
2. **Provider registry + endpoint health/quarantine**.
3. **Scenario model** with explicit authorized scope.
4. **Verify/diagnostic vs active-test separation**.
5. **Failure classification + retry policy**.
6. **Structured run/evidence model**.
7. **DNS SPF/DKIM/DMARC enrichment**.
8. **TLS/certificate enrichment**.
9. **Console/JSON/Markdown/HTML renderers**.
10. **Reproducible run artifacts and replay**.
11. **Controlled multi-target/multi-transport execution**.
12. **Additional security robustness tests**.

Before every implementation step, inspect the current `main` interfaces. Do not invent parallel abstractions when an existing `load2` type already provides the required responsibility.

---

## 8. Audit status and evidence rule

The repository list is intentionally preserved even when a source-level audit is not yet complete. This prevents losing research leads.

Status meanings:

- **AUDITED** — README/metadata and relevant source structure inspected; concrete transfer candidates identified.
- **REFERENCE / compare** — repository is useful for comparison but no direct implementation decision has been made.
- **AUDIT LATER** — repository is recorded but implementation-level inspection is still required.
- **ADOPTED** — functionality has actually been implemented in `load2` and is backed by tests/CI.

No repository becomes a source of truth merely because it appears in this document.

---

## 9. Current conclusion

The most valuable external ideas for `load2` are not the raw "bombing" operations. They are the systems around them:

**provider discovery → normalization → health → bounded scheduling → transport execution → failure classification → retry/quarantine → enrichment → evidence → reporting → exit status → reproducible run**.

The offensive repositories are still worth retaining because they expose real-world approaches to high-throughput orchestration, provider fan-out, failure handling and scenario execution. We should extract those mechanisms and, where an active capability is genuinely useful for security testing, implement it as an explicitly bounded and authorized test mode rather than as an unrestricted abuse tool.
