# load2 — External mechanism gap matrix

**Authority:** `sirvan0010-alt/load2` / `main`  
**Baseline CI:** commit `58ff04a` · CI #159 success · CodeQL #44 success  
**Decision rule:** mechanism usefulness for **authorized, user-specified SMTP/load testing** — not repository label.

Related: `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md`, `FEATURE-BACKLOG.md`.

---

## 1. Decision vocabulary

| Tag | Meaning |
|-----|---------|
| **HAVE** | Already in load2 at acceptable quality |
| **HAVE-WEAK** | Present but external patterns suggest HARDEN |
| **GAP** | Missing; candidate ADOPT/ADAPT |
| **SIMULATE** | Only as lab/test-double against controlled fixtures |
| **REJECT** | Does not belong in this product |

---

## 2. Mechanism inventory vs load2

| Mechanism class | External signal | load2 today | Verdict | Target in load2 |
|-----------------|-----------------|-------------|---------|-----------------|
| Bounded worker pool | scanner/bomber thread pools | `Channel` + `MaxConcurrency` workers | **HAVE** | `SmtpTestRunner` |
| Global send pacing | burst/throttle schedulers | `SmartPaceController.AcquireSendSlotAsync` | **HAVE** | pacing before `SendAsync` |
| Adaptive concurrency | error-rate scaling | `AdaptiveConcurrencyLimiter` | **HAVE** | runner pipeline |
| Circuit breaker | open on failure rate | `CircuitBreaker` | **HAVE** | optional path |
| Per-recipient limit | per-target quotas | `SmartPace` recipient windows | **HAVE** | options |
| Proxy rotation + ban | dead proxy skip | `ProxyRotator.ReportBlocked` | **HAVE** | BUG-004 fixed |
| IPv4/IPv6 source rotation | endpoint rotation | `IpV4Rotator` / `IpV6Rotator` | **HAVE** | RFC 3021 /31/32 |
| SMTP connection pool + health | session reuse | `SmtpConnectionPool` + idle NOOP check | **HAVE** | pool |
| Delivery ledger / no-dup restart | dedupe successful work | `DeliveryLedger` + AutoRestart | **HAVE** | BUG-002 |
| Retry through same gate | retry storms control | retry → `AcquireSendSlot` | **HAVE** | BUG-007/009 |
| Cancellation / permit ownership | cancel mid-flight | stress tests adaptive+pace | **HAVE** | CI #147 |
| Direct MX single-domain | multi-domain fan-out | `DirectMxRouting` + validation | **HAVE** | BUG-008 |
| Plugin payload pipeline | POC plugins | `IMailPayloadPlugin` + loader + PathSecurity | **HAVE** | plugins dir |
| Path / reparse safety | — | `PathSecurity` on EML/log/cert/profile/plugin | **HAVE** | SEC path |
| AUTH log redaction | — | `ProtocolLogRedaction` + MailKit detector wire | **HAVE** | SEC-001 |
| DryRun / TestMode / `--unauthorized` | — | validation + CLI/GUI gates | **HAVE** | safety boundary |
| **Transport latency breakdown** | telemetry in scanners | progress has limited split | **GAP** | FEAT-022 · `MailTestResult` / progress |
| **Endpoint/SMTP host health score** | dead-provider quarantine | proxy ban only; SMTP host soft-fail not scored | **GAP** | new `EndpointHealth` or pool metrics |
| **Structured run report export** | scanner HTML/JSON reports | dashboard + counters | **HAVE-WEAK** | JSON/CSV export API |
| **Run ID + replayable config snapshot** | RL/experiment runners | profiles only | **GAP** | profile + run manifest |
| **Response class taxonomy** | 4xx/5xx/greylist buckets | counters + `ObservedResponses` | **HAVE-WEAK** | expand classification |
| **Verify vs Execute plugin phases** | POC-bomber | plugins execute in send path | **GAP** | optional `VerifyAsync` on plugin |
| Provider auto-discovery (Shodan etc.) | open relay hunting | user-supplied host/MX only | **REJECT** | — |
| Multi-channel Discord/Telegram/SMS bomb | Beast_Bomber etc. | SMTP only | **REJECT** | — |
| CAPTCHA/OTP/stealth/credential theft | various bombers | — | **REJECT** | — |
| Unbounded public-target flood | default bomber UX | explicit targets + bounds | **REJECT** | — |
| Live IP/proxy/IPv6 matrix evidence | — | offline unit coverage | **SIMULATE** | NET-AUDIT-001 when fixtures exist |

---

## 3. Prioritized implementation backlog (from matrix)

Only items that strengthen **authorized SMTP load testing**:

| Prio | ID | Work | Effort | Depends |
|------|-----|------|--------|---------|
| 1 | **FEAT-022** | Split timings: queue wait / adaptive wait / pace wait / SMTP RTT / total | M | `SmtpTestRunner`, `MailTestResult` |
| 2 | **FEAT-HEALTH** | Soft SMTP endpoint health: consecutive failures → temporary skip/score in multi-MX or reconnect path | M | pool / MX list |
| 3 | **FEAT-REPORT** | Machine-readable run summary JSON (options hash, counts, latency percentiles, cancel flag) | S | result model |
| 4 | **FEAT-RUNID** | Stable `RunId` + optional write of options snapshot next to session log | S | runner start |
| 5 | **FEAT-VERIFY** | Optional plugin `VerifyAsync` before live send (schema/MIME dry checks) | M | `IMailPayloadPlugin` |
| 6 | NET-AUDIT-001 | Live matrix when lab endpoints available | L | fixtures |

---

## 4. Explicit non-goals (REJECT)

Do **not** schedule implementation for:

- credential/token harvesting, CAPTCHA/OTP bypass, stealth/evasion of provider abuse controls  
- automatic discovery of arbitrary third-party infrastructure for mass send  
- non-SMTP harassment channels (SMS/Discord/Telegram flood adapters)  
- removing or weakening `--unauthorized`, DryRun, TestMode, pacing, or concurrency bounds  

Studying those in external repos for **contrast** is fine; shipping them is not.

---

## 5. Per-repo deep-dive status

Full source-level folders under `docs/external-repos/` are filled as each repo is opened. Until then the summary table in `EXTERNAL-REPO-TRANSFER-AUDIT.md` stands.

| Repo (short) | Source deep-dive | Transfer posture |
|--------------|------------------|------------------|
| Beast_Bomber | pending | ADOPT architecture only (orchestrator/adapters); REJECT multi-channel flood |
| Bombers (collection) | pending | catalog of patterns; child projects one-by-one |
| POC-bomber | pending | ADOPT plugin verify/execute + isolation ideas |
| devops-kung-fu/bomber | pending | ADOPT report/SBOM-style result structure ideas |
| Email-Bomber / SMTP variants | pending | REFERENCE compare SMTP lifecycle only |
| smsbomb / sms-bomber | pending | REJECT SMS channel; optional abstract health metrics only |
| bomberman-rl | pending | ADOPT run-id / artifact layout ideas |
| others in audit table | pending | as listed |

---

## 6. Next concrete engineering step

**Implement FEAT-022 (timing breakdown)** in Core + tests, then CI — highest leverage gap that does not expand attack surface.
