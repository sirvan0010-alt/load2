# `devops-kung-fu/bomber`

- URL: https://github.com/devops-kung-fu/bomber
- Revision reviewed: `6a7f05aad6e6aca3e359f87f3155936484a6dae0`
- Branch: `main`
- Language: Go
- License: MPL-2.0
- Archived: no
- Repository description: SBOM vulnerability scanner
- Audit status: **COMPLETE**

## Scope

This repository is **not an email bomber**. It was audited because its provider, enrichment, filtering, structured-result and CI-oriented execution architecture contains mechanisms relevant to the planned `load2` provider/transport and diagnostics layers.

No scanner code or unrelated Go implementation is imported into `load2`.

## Source-level evidence

### Core scanner

`lib/scanner.go`, revision `6a7f05aad6e6aca3e359f87f3155936484a6dae0`, defines `Scanner` and the `Scan(args []string)` execution path. The observed pipeline is:

```text
Loader.Load
  -> detect ecosystems
  -> sanitize inputs
  -> Provider.Scan
  -> ignore/filter
  -> enrichment
  -> severity summary
  -> Results
  -> Renderer(s)
  -> optional exit code
```

The scanner owns provider, credentials, renderers, enrichment selections, ignore file, severity filter and output configuration. `Provider.Scan` is kept behind an interface rather than embedded in the scanner.

### Provider abstraction

`models/interfaces.go` defines `Provider` with:

- `SupportedEcosystems()`
- `Info()`
- `Scan(purls, credentials)`

This is concrete evidence for a capability-oriented provider abstraction.

### Input loader

`lib/loader.go` defines `Loader.Load`, supporting file and directory input plus `-` for stdin. It detects CycloneDX XML/JSON, SPDX and Syft SBOMs. It records SHA-256 for scanned files and removes duplicate package URLs/licenses.

### Input sanitization

`filters/purl.go` defines `Sanitize`. Invalid package URLs and `file:` package URLs are rejected into structured `models.Issue` entries instead of silently becoming executable work.

### Structured results

`models/structs.go` defines:

- `Package`
- `Vulnerability`
- `Summary`
- `Results`
- `Meta`
- `ScannedFile`
- `Credentials`
- `Issue`

`Results.Meta` contains generator, URL, version, provider, severity filter and timestamp. This is useful evidence for a future machine-readable `load2` run summary.

### Provider implementation

`providers/osv/osv.go` implements `Provider.Scan`. It builds a batched query, uses a reusable HTTP client/transport, calls OSV, hydrates the response and maps provider data into the common package/vulnerability model.

The source explicitly clones the default HTTP transport and configures a TLS handshake timeout. This is a useful transport-layer pattern, although `load2` must continue using its existing MailKit/MimeKit network architecture.

### Tests

`lib/scanner_test.go` contains a `MockProvider` implementing the provider interface and tests scanner behavior, input handling, enrichment, filtering and exit-code mapping. `filters/purl_test.go` tests sanitization.

## Mechanism inventory

| Mechanism | Exact source | load2 mapping | Decision |
|---|---|---|---|
| Provider interface | `models/interfaces.go` | Future transport/provider registry around SMTP, Direct MX and diagnostics | **ADOPT architecture** |
| Provider capability declaration | `Provider.SupportedEcosystems()` | Transport capabilities / supported modes | **ADAPT** |
| Provider metadata | `Provider.Info()` | Endpoint/provider identity and diagnostics | **ADOPT** |
| Provider-specific execution behind interface | `Provider.Scan()` | Keep network execution separate from payload generation | **ADOPT** |
| Reusable HTTP transport/client | `providers/osv/osv.go` | General transport/session reuse principle | **EXTRACT** |
| Input validation/sanitization | `filters/purl.go` | Target/endpoint canonicalization and validation | **HARDEN** |
| Structured issue collection | `models.Issue` | Formal failure/validation classification | **ADAPT** |
| Duplicate removal | `Loader.Load` | Endpoint/target canonicalization and deduplication | **ADAPT** |
| File SHA-256 evidence | `Loader.loadFilePurls` | Run/input evidence where useful | **ADAPT** |
| Directory + stdin input | `Loader.Load` | Input-source abstraction for scenarios | **REFERENCE / ADAPT** |
| Filter stage | `Scanner.filterVulnerabilities` | Result/failure policy pipeline | **ADAPT** |
| Ignore stage | `Scanner.ignoreVulnerabilities` | Explicit suppression/allowlist policy layer | **REFERENCE / ADAPT** |
| Enrichment stage | `Scanner.enrichVulnerabilities` | DNS/TLS/email-security enrichment pipeline | **ADOPT architecture** |
| Structured `Results` object | `models.Results` | Machine-readable run summary | **ADOPT architecture** |
| Renderer abstraction | `Scanner.Renderers` | Console/JSON/Markdown/HTML separation | **ADOPT architecture** |
| Severity-based exit code | `exitWithCodeIfRequired` | CLI exit-code taxonomy | **ADAPT only after CLI audit** |
| Mock provider testing | `lib/scanner_test.go` | Fake SMTP/provider tests | **ADOPT** |
| Credentials object | `models.Credentials` | Secret provenance abstraction | **REFERENCE / HARDEN**; do not persist secrets |
| OpenAI enrichment | `enrichers` / scanner | Optional external enrichment concept | **REFERENCE**; not required for core mail engine |

## Detailed findings

### 1. Provider → enrichment → filter → renderer pipeline

**Mechanism:** The scanner keeps acquisition, filtering, enrichment and presentation as separate stages.

**Why useful for `load2`:** The same separation fits the desired architecture:

```text
SMTP / Direct-MX transport result
        ↓
normalization / classification
        ↓
DNS + TLS + SMTP-security enrichment
        ↓
policy/filter stage
        ↓
structured RunResult
        ↓
renderer
```

**Decision: ADOPT architecture.**

Required `load2` constraint: network operations remain in the network/transport layer; MIME generation remains in `IMailPayloadPlugin` and related payload components.

### 2. Provider abstraction and capabilities

The `Provider` interface gives the scanner a stable execution boundary while allowing multiple providers with different capabilities.

For `load2`, this supports a future registry for SMTP providers/transports without forcing all transports into one implementation. The exact interface must be designed from the existing `load2` types rather than copied from Go.

**Decision: ADOPT architecture / ADAPT interface.**

### 3. Input validation and deduplication

`filters.Sanitize` converts invalid inputs into structured issues. `Loader.Load` removes duplicate package URLs and licenses.

For `load2`, the corresponding mechanism should be target and endpoint canonicalization before work enters the bounded queue. This complements the existing delivery ledger, proxy quarantine and Direct-MX validation.

**Decision: HARDEN / ADAPT.**

### 4. Reusable transport

The OSV provider creates a reusable Resty client and cloned HTTP transport rather than constructing a fresh transport for each package query.

The transferable lesson is connection/transport reuse and explicit transport configuration. `load2` already has persistent SMTP sessions, so this reinforces the current architecture rather than replacing it.

**Decision: EXTRACT.**

### 5. Structured results and evidence

The `Results` model groups metadata, scanned files, licenses, severity summary and packages. This is a strong pattern for the planned `load2` machine-readable run summary.

A `load2` equivalent should additionally contain RunId/config snapshot, target/transport, attempts, timing, SMTP response classification and optional DNS/TLS evidence, with secrets redacted.

**Decision: ADOPT architecture.**

### 6. Enrichment

The scanner can enrich provider results after the primary scan. The architecture is directly transferable to DNS/TLS/email-security diagnostics.

Candidate future pipeline:

```text
SMTP result
 -> DNS enrichment (MX/SPF/DKIM/DMARC)
 -> TLS enrichment
 -> security-policy classification
 -> final evidence
```

**Decision: ADOPT architecture.**

### 7. Renderer separation

The scanner does not make the provider responsible for final presentation; renderers consume a common `Results` object.

This matches the `load2` requirement for console/UI/JSON/report output without coupling it to SMTP code.

**Decision: ADOPT architecture.**

### 8. Exit codes

The scanner can return an exit code derived from highest severity. This proves the pattern but does not establish what `load2` should use.

`load2` must first audit its current CLI compatibility and then define any new taxonomy without breaking existing behavior.

**Decision: ADAPT, pending CLI audit.**

### 9. Testable provider boundary

The repository uses a `MockProvider` in scanner tests. This is particularly useful for `load2`: orchestration tests should be able to replace real SMTP/Direct-MX transports with deterministic fakes while exercising queueing, pacing, retry, cancellation, classification and rendering.

**Decision: ADOPT.**

## Concurrency / retry / timeout findings

The inspected core scanner path does not provide evidence for a sophisticated worker-pool or retry architecture comparable to the current `load2` SMTP engine. The OSV provider uses a batched request, and the HTTP transport has a TLS handshake timeout, but this audit does **not** establish a general cancellation-aware worker scheduler in the repository.

Therefore:

- do not claim this repository as evidence for bounded worker concurrency;
- do not copy its timeout behavior into SMTP sending;
- keep `load2`'s existing `Channel` worker pool, adaptive concurrency, pacing gate and `CancellationToken` model as authoritative.

## Security-relevant observations

- Credentials are passed through a model object to providers.
- The inspected result model does not include credentials in its JSON result fields.
- Input validation creates explicit issue records for invalid PURLs.
- The repository contains external-provider integrations and optional OpenAI enrichment; these should remain opt-in and isolated in `load2`.
- No hardcoded SMTP credentials or email delivery mechanism was found in the inspected source because this repository is a vulnerability/SBOM scanner, not a mail sender.

## Do not import

- Go implementation details merely because they are shorter than existing C# code.
- SBOM-specific data models as `load2` runtime models.
- OSV provider endpoints as part of the SMTP delivery path.
- OpenAI enrichment as a core dependency.
- The repository's severity exit-code numbers as `load2` CLI semantics without a compatibility audit.
- Credentials or provider secrets into persisted run artifacts.

## Recommended load2 actions

1. Add/complete a transport/provider capability registry using existing `load2` abstractions.
2. Add endpoint/target canonicalization and deduplication before queue admission.
3. Define a structured `RunResult`/evidence model independently of renderers.
4. Add enrichment stages for DNS and TLS without coupling them to MIME generation.
5. Add fake-provider/fake-SMTP seams for orchestration tests.
6. Audit current CLI exit codes before introducing new values.

## Required tests before implementation

- Provider capability selection chooses only compatible transports.
- Duplicate canonical endpoints are executed once.
- Invalid targets become structured validation failures and never enter the network queue.
- Fake provider execution remains cancellation-safe.
- Renderer failures do not corrupt transport results.
- Sensitive credential fields never enter JSON run artifacts.
- DNS/TLS enrichment can fail independently without losing the primary SMTP result.
- Exit-code changes preserve current CLI compatibility.

## Evidence

- Repository: `devops-kung-fu/bomber`
- Revision: `6a7f05aad6e6aca3e359f87f3155936484a6dae0`
- Branch: `main`
- Repository metadata verified: Go, MPL-2.0, active/non-archived.
- Files inspected:
  - `lib/scanner.go`
  - `lib/loader.go`
  - `models/structs.go`
  - `models/interfaces.go` (via source search)
  - `providers/osv/osv.go`
  - `filters/purl.go`
  - `lib/scanner_test.go`
- Review basis: source-level inspection, not README-only classification.
