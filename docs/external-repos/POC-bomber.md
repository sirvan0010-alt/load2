# tr0uble-mAker/POC-bomber

- URL: https://github.com/tr0uble-mAker/POC-bomber
- Revision reviewed: `d2433ac41eaa58eb4fb0876ec05e3b645e10ecd7` (`main`)
- License / archived?: Public repository; GitHub metadata reports `archived=false`. README identifies it as a Python POC/EXP integration framework.
- Entry point: `pocbomber.py` → `inc.console.pocbomber_console()`
- Stack: Python 3; `concurrent.futures`; `queue`; `func_timeout`; `requests`; `dnslib`; WSGI simple server.
- Audit status: COMPLETE for the core execution/orchestration modules inspected below.

## Mechanisms of interest

| Mechanism | File | Symbol | Inputs | Network behavior | Concurrency | Failure/retry | load2 mapping | Decision |
|---|---|---|---|---|---|---|---|---|
| Verify/Attack separation | `inc/run.py` | `verify()`, `attack()` | target list, POC names | POC modules perform their own network actions | verify dispatches through pool; attack is direct | broad exception returns false | separate verification from execution/scenario action | ADAPT |
| Bounded worker pool | `inc/thread.py` | `ThreadPool` | `max_threads`, target×POC tasks | POC-dependent | `ThreadPoolExecutor(max_workers=...)` | future-based result collection | existing bounded worker/channel model | ADOPT/HARDEN |
| Target×POC batch expansion | `inc/run.py`, `inc/thread.py` | `verify()`, `add_task()` | target list + script list | one verification task per pair | bounded executor | per-task timeout result | scenario/task matrix | ADAPT |
| Per-POC timeout | `inc/thread.py` | `set_fuc_timeout()` | configured timeout | wraps POC verify call | worker-local | timeout converted to result | `CancellationToken` + operation timeout | ADAPT |
| Delay serialization | `inc/thread.py`, `inc/console.py` | `start_threadpool()` | `delay` | no special transport | when delay set, console forces `max_threads=1` and waits each future | simple sleep | global/per-scope pacing, without serializing whole workload | HARDEN |
| Dynamic POC discovery | `inc/common.py` | `get_poc_modole_list()`, `get_pocinfo_dict()` | `pocs` tree | imported Python modules | sequential discovery | import failures silently ignored | `IMailPayloadPlugin`/plugin loader concept | EXTRACT |
| POC selection by name | `inc/common.py` | `get_poc_scriptname_list_by_search()` | path + script names | none | sequential | missing/failed modules logged | plugin registry/selection | ADAPT |
| Target-file parsing | `inc/common.py` | `get_target_list()` | file path | URLs only by regex | sequential | read failure returns empty list | target-set abstraction + scope validation | ADAPT/HARDEN |
| Structured result queue | `inc/output.py` | `put_output_queue()`, `output_result()` | result dictionaries | none | producer/consumer queue | timeout represented in result | delivery/run result ledger | ADAPT |
| DNS-log correlation | `inc/dnslog.py` | `dnslog_add_scan()`, `dnslog_scan()` | DNS token/domain + result | HTTP polling + local DNS/HTTP servers | dedicated threads | polling and expiry-like visit counter | future DNS evidence enrichment | ADAPT/SIMULATE |
| Explicit attack switch | `inc/common.py`, `inc/console.py` | `--attack`, `run.attack()` | target + POC | invokes POC `attack()` | direct | boolean success/failure | authorized Execute phase with explicit gate | ADAPT |
| Unrestricted EXP invocation | `inc/run.py`, `inc/console.py` | `attack()` | target/POC | POC-specific attack | direct | broad catch | no direct import | REJECT |

## Detailed findings

### 1. Verify vs Attack boundary
`inc/run.py` exposes two distinct paths. `verify(target_list, script_list)` builds a thread pool and queues every target/POC pair for `verify`; `attack(target, script)` directly invokes the selected POC's `attack()` method and converts the result to a boolean. `inc/console.py` runs verification first and only checks `--attack` afterward. This is a concrete separation between detection/verification and an active execution path. fileciteturn384file0L2-L2 fileciteturn390file0L2-L2

**load2:** ADAPT. Preserve the architectural boundary, but make the execute phase an explicit authorized scenario/action with the existing `--unauthorized` requirement, scope checks, cancellation and rate/concurrency controls. Do not import arbitrary POC EXP execution.

### 2. Bounded concurrency and target×POC matrix
`ThreadPool` constructs `ThreadPoolExecutor(max_workers=self.max_thread)`. `verify()` creates one queued task for each target/script pair. Results are either consumed as each future completes or, when delay is configured, processed synchronously with a sleep between results. `inc/console.py` additionally forces `max_threads=1` whenever `delay` is enabled. fileciteturn385file0L2-L2 fileciteturn384file0L2-L2 fileciteturn390file0L2-L2

**load2:** ADAPT/ADOPT. The underlying bounded-worker principle matches load2's existing `Channel` worker model. The target×plugin matrix is useful as a future scenario abstraction. The repo's delay implementation is not suitable as-is because it serializes the workload instead of pacing actual network sends.

### 3. Timeout behavior
`run_signel_poc()` wraps each POC verification call in `set_fuc_timeout()`, decorated with `func_set_timeout(common.get_value('timeout'))`. Timeout/error is converted into a result containing `url`, `script` and `timeout=True`. fileciteturn385file0L2-L2

**load2:** ADAPT. Keep operation-level timeouts but use `CancellationToken` and typed timeout/failure classification so cancellation can propagate through SMTP/DNS/TLS operations and resource leases are always released.

### 4. Dynamic POC/plugin discovery
`common.py` recursively enumerates the `pocs` directory and imports modules dynamically. `get_pocinfo_dict()` retains modules exposing `verify`; `get_poc_scriptname_list_by_search()` selects modules by filename. fileciteturn386file0L2-L2

**load2:** EXTRACT/ADAPT. The useful mechanism is a discoverable plugin registry, not Python module importing. Map it to the existing `IMailPayloadPlugin` architecture and current hardened plugin loader, with path/reparse and trust controls retained.

### 5. Target inventory and scope
`get_target_list()` reads a file and accepts entries matching an HTTP(S) URL regex. The console then reports the number of targets and POCs before running the matrix. fileciteturn386file0L2-L2 fileciteturn390file0L2-L2

**load2:** ADAPT/HARDEN. A target-set abstraction is useful, but load2 must canonicalize/deduplicate targets and enforce explicit authorization/scope before any network action. Do not import the weak regex-only scope model.

### 6. Structured results and output pipeline
The scanner uses an output queue. `output_result()` increments counters, recognizes timeout/error/vulnerable outcomes, appends successful results to a success list and persists them via `data_save()`. Results contain fields such as URL, script, name and vulnerability state. fileciteturn388file0L2-L2

**load2:** ADAPT/HARDEN. This supports the existing move toward structured run results, delivery ledger and machine-readable run summaries. A typed result model is preferable to mutable global dictionaries.

### 7. DNS-log evidence channel
`dnslog.py` generates random subdomains, records DNS observations, correlates observed domains with pending scan results, marks matching results vulnerable, and polls a small HTTP endpoint. It also contains a local DNS server and WSGI HTTP server for collecting observations. The implementation uses dedicated threads and polling loops. fileciteturn387file0L2-L2

**load2:** ADAPT/SIMULATE. The transferable mechanism is asynchronous external evidence correlation: create a run-scoped token, collect DNS observations, correlate them to a pending operation, and emit evidence. For load2 this belongs in a future DNS/security-evidence subsystem, not in the SMTP sending hot path. The original public/attack-oriented DNS-log workflow is not imported.

### 8. Explicit delay/rate behavior
When `delay` is non-zero, the console sets maximum concurrency to one and `start_threadpool()` waits for each future before sleeping. fileciteturn390file0L2-L2 fileciteturn385file0L2-L2

**load2:** HARDEN/ADAPT. This is evidence that pacing and concurrency should be separate controls. load2 already has stronger actual-SEND pacing and bounded concurrency; do not regress to global serialization.

## Do not import

- Arbitrary POC `attack()` / EXP execution against supplied targets: **REJECT** as a direct feature import. The architectural verify/execute boundary is useful; unrestricted exploit execution is not.
- Silent broad-exception error handling: **HARDEN** rather than copy.
- Regex-only target authorization: **REJECT** as a scope model; replace with canonicalization and explicit authorization.
- Infinite/polling DNS-log lifecycle without cancellation: **REJECT** as implementation pattern; use cancellation-aware bounded evidence collection.
- Python dynamic imports: **REFERENCE/EXTRACT** only; use load2's existing plugin architecture and filesystem security controls.

## Proposed load2 follow-up

1. Add a formal `Verify`/`Execute` phase distinction to the scenario model once the scenario engine gap is implemented.
2. Model target×plugin as a bounded work matrix without creating an unbounded task per matrix item.
3. Standardize typed `RunResult` / `Evidence` records for delivery, DNS and TLS checks.
4. Add cancellation-aware external-evidence correlation as a separate subsystem if DNS/MX/SPF/DKIM/DMARC enrichment is implemented.
5. Keep actual-SEND pacing after worker/concurrency/session admission; do not adopt the source repo's serial delay behavior.

## Evidence

- Repository revision: `d2433ac41eaa58eb4fb0876ec05e3b645e10ecd7`.
- Files inspected: `pocbomber.py`, `inc/console.py`, `inc/run.py`, `inc/thread.py`, `inc/common.py`, `inc/dnslog.py`, `inc/output.py`.
- Audit date: 2026-09-13.
- Source-level conclusion: **COMPLETE** for the core orchestration, verification/attack boundary, timeout, plugin discovery, target inventory, result pipeline and DNS-log evidence modules inspected.
- No load2 stress/spam/attack module was added by this audit.
