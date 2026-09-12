# load2 — External mechanism gap matrix

**Authority:** `sirvan0010-alt/load2` / `main`

## FEAT-022 — IMPLEMENTED

Phase timing averages on `MailTestResult` (successful messages):

| Field | Meaning |
|-------|---------|
| `AvgPrepWaitMs` | `WaitBeforeSendAsync` (absolute/recipient gates) |
| `AvgAdaptiveWaitMs` | rate limiter + adaptive acquire |
| `AvgPoolWaitMs` | `SmtpConnectionPool.RentAsync` |
| `AvgPaceWaitMs` | `AcquireSendSlotAsync` |
| `AvgSmtpSendMs` | `SmtpClient.SendAsync` |

Commits: `d0af677` (runner+model) · tests `e8732d6` · `TimingBreakdownTests`

---

## Remaining backlog (authorized SMTP only)

| Prio | ID | Work |
|------|-----|------|
| 2 | FEAT-HEALTH | Soft SMTP endpoint health score |
| 3 | FEAT-REPORT | JSON run summary |
| 4 | FEAT-RUNID | RunId + config snapshot |
| 5 | FEAT-VERIFY | Plugin VerifyAsync |
| — | NET-AUDIT-001 | Live matrix when fixtures exist |

## REJECT
credential theft, CAPTCHA/OTP bypass, stealth, multi-channel bomb, auto public-target discovery.
