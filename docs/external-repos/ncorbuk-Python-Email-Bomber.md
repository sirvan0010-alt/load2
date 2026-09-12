# External Repo Audit — ncorbuk/Python---Email-Bomber

Status: **SOURCE-AUDITED**

## Source of truth inspected
- Repository: `ncorbuk/Python---Email-Bomber`
- Inspected revision: `727a38779e14330b4b12d60f4261a35314543926`
- Execution source: `Email_Bomber.py`
- README and source were inspected; this audit is based on source behavior, not repository name alone.

## Verified mechanisms
- `Email_Bomber.email()` collects SMTP server/port, sender identity, credentials, subject and message, then establishes an SMTP session with `smtplib.SMTP`.
- Explicit SMTP lifecycle: EHLO → STARTTLS → EHLO → LOGIN → send operation.
- `Email_Bomber.send()` performs the send and increments the send counter.
- `Email_Bomber.attack()` performs a finite `range(self.amount + 1)` send loop and closes the SMTP session afterwards.
- Server/port/provider selection is configurable in the interactive flow.
- No cancellation token, bounded worker model, structured failure classification or global pacing model is present.
- Credentials are entered interactively.
- The operational loop is an email-bombing loop and is not suitable for direct import as an unrestricted public-target feature.

## Transfer matrix into load2

| Mechanism | Decision | load2 integration | Rationale |
|---|---|---|---|
| Persistent SMTP session | ADOPT / HARDEN | SMTP session/pool layer | Reuse transport instead of reconnecting for every message. |
| Configurable SMTP server + port | ADAPT | SMTP transport configuration | Keep endpoint configuration separate from payload generation. |
| Explicit STARTTLS phase | ADAPT | MailKit/MimeKit SMTP transport | Preserve explicit TLS negotiation and policy validation. |
| Session setup vs. send separation | ADAPT | SMTP runner/session abstraction | Keeps connection lifecycle separate from message-generation logic. |
| Send counter | HARDEN | Structured delivery/result ledger | Counters must distinguish success, failure and cancellation. |
| Finite count | ADAPT | Existing bounded scenario/message count | Useful as a controlled test parameter with authorization and limits. |
| Interactive credentials | REJECT pattern | Environment/secret-provider configuration | No passwords in CLI prompts, source or scenario files. |
| Uncontrolled bombing loop | REJECT as operational behavior | None | Do not import unrestricted public-target abuse behavior. |
| No cancellation/pacing | HARDEN | CancellationToken + SmartPaceController | load2 requires cooperative cancellation and centralized pacing. |

## Explicit adoption list
1. Keep persistent SMTP-session reuse as a first-class transport optimization.
2. Keep SMTP server/port as transport configuration, not hardcoded provider logic.
3. Preserve explicit STARTTLS as a transport phase mapped to MailKit TLS policy.
4. Keep session establishment separate from payload/message generation.
5. Represent send outcomes through load2's structured result/ledger rather than a raw integer counter.
6. Use finite message counts only inside the existing authorized, bounded execution model.

## Explicit non-adoption
- Interactive password collection.
- Hardcoded provider credentials.
- Unrestricted bombing loops.
- Direct import of third-party public-target abuse behavior.

## Relationship to load2 architecture
The transferable engineering value is the SMTP lifecycle and separation of session setup from individual sends. In load2 these mechanisms must remain behind the existing bounded Channel worker model, `SemaphoreSlim`/adaptive concurrency controls, `SmartPaceController`, `CancellationToken`, delivery ledger and authorization gate.
