# un1cum/Beast_Bomber

## Status
- Audit: PARTIAL — source-level entry point inspected; subsystem modules require separate focused review before any implementation decision.
- Revision: `main` at the inspected repository state; entry-point blob `789a96823b059a84282c8c7939530c956a6db6d4`.
- load2 authority remains `sirvan0010-alt/load2` / `main`.

## Entry points
- `beast.py` — top-level interactive dispatcher.
- The dispatcher constructs separate subsystem objects for SMS, DDoS, email, Discord and Telegram functions and routes menu selections to those objects.
- The repository tree exposes separate `core/` and `input/` areas.

## Source map
`beast.py` imports separate subsystem classes and a settings component. The top-level design is therefore a capability dispatcher rather than a unified transport engine.

Evidence: `beast.py` directly instantiates `SMSAttack`, `DDoSAttack`, `EmailAttack`, `DiscordSpam`, `TelegramAttack` and `Settings`.

## Execution trace
```text
interactive menu
  → capability selection
  → subsystem object
  → subsystem-specific execution
  → local console result/error handling
```

The inspected entry point does not provide evidence of a common queue, common pacing layer, common retry taxonomy, shared cancellation abstraction or shared result model across the capabilities. Those mechanisms remain unknown until the individual subsystem sources are audited.

## Mechanisms
| Mechanism | Evidence | load2 mapping | Decision |
|---|---|---|---|
| Capability-oriented dispatcher | `BeastBomber.main()` routes menu options to separate subsystem objects | Scenario/capability orchestration layer | ADAPT |
| Separate capability modules | `core/` contains protocol/capability-specific modules imported by the entry point | Plugin/domain separation | REFERENCE / ADAPT |
| Central settings object | `Settings()` is created once by the dispatcher | Existing configuration/options model | REFERENCE |
| Interactive execution loop | menu + recursive return to `main()`/`ex()` | CLI/UI concern, not engine scheduling | REFERENCE |
| Common bounded queue | Not evidenced in inspected entry point | Existing bounded `Channel<T>` is authoritative | PENDING |
| Common pacing/rate limiter | Not evidenced in inspected entry point | Existing `SmartPaceController` is authoritative | PENDING |
| Common retry model | Not evidenced in inspected entry point | Existing `RetryPolicy` is authoritative | PENDING |
| Common observability/result model | Not evidenced in inspected entry point | `MailTestResult` / `RunReport` / `RunObservability` | PENDING |

## Security / abuse-specific behavior
The repository explicitly contains capabilities named for DDoS and spam/bombing. Those capability implementations are not copied or treated as implementation templates. The useful extraction target here is the architectural separation of capabilities, not operational abuse behavior.

## Tests required
Before adopting any dispatcher/plugin concept into load2:

1. prove the capability is represented by a typed load2 abstraction;
2. preserve the existing bounded queue and pacing pipeline;
3. verify `CancellationToken`, `DryRun/TestMode`, `--unauthorized` and hard limits remain effective;
4. add focused tests for registration, selection, lifecycle and failure isolation;
5. verify CI and CodeQL.

## Conclusion
`Beast_Bomber` provides a useful architectural reference for **capability separation**, but the inspected entry point does not justify importing its execution model. Further extraction should inspect the individual subsystem sources only for generic scheduling, lifecycle, configuration, diagnostics and failure-handling mechanisms that can be mapped into the existing load2 abstractions.
