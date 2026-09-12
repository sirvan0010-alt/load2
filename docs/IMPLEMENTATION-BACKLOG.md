# load2 — SMTP-first Implementation Backlog

**Authority:** source + tests on `main`.

## Priority 0 — safety (unchanged)
Authorization boundaries, no opaque MSBuild loaders, no force-push CI.

## Priority 1 — FEAT-HEALTH v1 — DONE
`TransportHealthRegistry` + runner record success/final failure. `IsAvailable` not yet used for selection.

## Priority 2 — FEAT-REPORT v1 — DONE (pending green CI on tip)

| Piece | Status |
|-------|--------|
| `RunReport` / `RunReportOptions` / `RunReportBuilder` | ✅ no passwords in sanitized options |
| `MailTestResult.RunId` | ✅ |
| `MailTestResult.EndpointHealth` | ✅ snapshots at end of run |
| Deterministic JSON (`ToJson`) | ✅ sorted health keys |
| Tests secret redaction / empty / dry-run | ✅ `RunReportTests` |
| Built on existing `MailTestResult` | ✅ not a parallel metrics system |

**Usage:** after `RunAsync`, `RunReportBuilder.Create(result.RunId!, started, finished, options, result, result.EndpointHealth)` then `ToJson`.

## Priority 3+
Multi-account session pool · health-based endpoint selection · connection-churn scenario · provider registry refinement

## Out of SMTP-first scope
SMS/WhatsApp/Call senders · public OTP endpoints · unrestricted flood tools
