# bhattsameer/Bombers — source-level mechanism audit

**Audit:** EXT-AUDIT-001  
**Status:** SOURCE-AUDITED — inspected email, SMS, browser-automation and local GUI execution modules; no load2 stress/spam module added.  
**Repository:** `bhattsameer/Bombers`  
**Reviewed ref:** `master`  
**Repository state:** archived; default branch `master`.  

## Audit rule

This audit is mechanism-level. The repository's offensive purpose does not cause every mechanism to be rejected. Useful engineering patterns are mapped to `load2` individually. No unrestricted bomber, public-target flooder, verification-bypass or credential-abuse path is imported.

## Source inventory inspected

The following execution sources were inspected directly:

- `Email_bomber.py` — blob `1c1674e3086f2d7c4f4e4573605e2a3fad424773`
- `Email_Bomber_Version2.py` — blob `55559334dbb2e70546d4df163a28f145a0857125`
- `spam.py` — blob `663242a0ff46a36967272300143590b65bb5d0e0`
- `SMS_bomber.py` — blob `c9e08dca9086f6e715e42a236192f71c198f8d`
- `SMS_bomber_version2.py` — blob `44dd7e903b94068c6be9b001a7764456e9696018`
- `sms_bomber_updated.py` — blob `566a5974969872a7a8efee6a962eff10e10c04a6`
- `numspy_bomber.py` — blob `76c6bfffe7fd1de09b18f38e9798a0a0e071503d`
- `Twitter_bomber.py` — blob `8fd84b31e7c003bf2f2bf37f90d3e140e60a40ee`
- `wbomb.py` — blob `576ae58bf8096f94bcac25fbf9dec6126460c9a2`

The repository README identifies the collection as SMS/email/WhatsApp/Twitter/Instagram bomber utilities and also lists temporary-number/fake-SMS utilities. That README is inventory evidence, not source-level evidence for implementation behavior.

## 1. `Email_bomber.py`

Exact source uses one SMTP object, selected as Gmail or Outlook, then repeatedly performs EHLO, STARTTLS, LOGIN and `sendmail` for a requested count, with a one-second sleep after each send. fileciteturn372file0L2-L6

| Mechanism | load2 mapping | Decision |
|---|---|---|
| explicit target | authorized target model + scope validation | HARDEN |
| finite count | bounded scenario/message count | ADAPT |
| Gmail/Outlook provider selection | provider/transport registry candidate | ADAPT |
| SMTP connection reused for loop | persistent SMTP session/pool | ADOPT/HARDEN |
| STARTTLS | existing MailKit TLS mapping/evidence | ADAPT |
| re-authentication every send | avoid where authenticated session remains valid | HARDEN |
| fixed `sleep(1)` | actual-SEND pacing gate | ADAPT |
| broad exception | structured failure taxonomy | HARDEN |
| interactive password | environment/secret-provider configuration | REJECT pattern |

### Architecture finding

This is the strongest SMTP mechanism in the repository: a connection is established once and reused for repeated operations. That aligns with load2's persistent-session architecture. The pacing and authentication placement should not be copied literally.

## 2. `Email_Bomber_Version2.py`

Exact source reads `email.txt`, then performs a nested target × count loop and creates a new SMTP connection for each attempted message before EHLO/STARTTLS/LOGIN/send. fileciteturn371file0L2-L6

| Mechanism | load2 mapping | Decision |
|---|---|---|
| file-backed target set | explicit target-set input abstraction | ADAPT |
| target × count iteration | bounded scenario | ADAPT |
| connection per message | controlled connection-churn benchmark only | SIMULATE |
| SMTP/TLS/auth sequence | MailKit transport layer | ADAPT |
| fixed sleep | actual-SEND pacing | ADAPT |
| local target file | canonicalization/dedup/scope validation | HARDEN |

The connection-per-message path is useful only as a deliberately bounded performance/robustness comparison against the persistent-session baseline.

## 3. `spam.py`

This is local GUI/keyboard automation rather than SMTP. It uses `pyautogui`, clipboard paste and keyboard submission. It supports file-backed messages, repeated text, generated numeric messages, explicit intervals and finite counts; zero is treated as infinite repetition in two modes. fileciteturn373file0L2-L6

| Mechanism | load2 mapping | Decision |
|---|---|---|
| deterministic payload file | payload/plugin source | ADAPT |
| explicit interval | scenario rate policy | ADAPT |
| finite count | bounded scenario | ADOPT |
| infinite repetition | cancellation/deadline stress scenario | SIMULATE |
| GUI automation | outside SMTP/network layer | REFERENCE |
| blocking sleep | asynchronous pacing/admission | HARDEN |

The important transferable abstraction is **payload source + rate + count/duration**, not GUI automation.

## 4. `SMS_bomber.py`

The source constructs a URL containing the supplied mobile number, creates an HTTP request with a browser-like User-Agent, repeats it for a requested count and sleeps between attempts. fileciteturn376file0L2-L6

| Mechanism | load2 mapping | Decision |
|---|---|---|
| explicit endpoint/target parameter | explicit authorized endpoint scope | HARDEN |
| HTTP request transport | transport abstraction concept | EXTRACT |
| finite repeat count | bounded scenario | ADAPT |
| configurable sleep/throttle | centralized pacing policy | ADAPT |
| custom headers | transport metadata/payload plugin concept | REFERENCE |
| hard-coded public service endpoint | no public-service flood target | REJECT |
| broad exception | typed failure result | HARDEN |

The HTTP mechanics are useful as a transport comparison, but the concrete public SMS endpoint is not transferable.

## 5. `SMS_bomber_version2.py`

The source defines two concrete HTTP endpoints and iterates over them for each requested count, creating requests and sleeping between requests. fileciteturn377file0L2-L6

| Mechanism | load2 mapping | Decision |
|---|---|---|
| multiple endpoint list | provider/endpoint registry concept | ADAPT |
| endpoint fan-out | bounded worker/scenario fan-out | ADAPT |
| finite repetition | scenario count | ADAPT |
| endpoint rotation | health-aware endpoint selection/quarantine | ADAPT conceptually |
| hard-coded third-party endpoints | public-target abuse path | REJECT |

The useful abstraction is **multiple transport endpoints behind one scenario**, not the specific SMS services.

## 6. `sms_bomber_updated.py`

This source is a simplified variant of `SMS_bomber.py`: one concrete HTTP endpoint, explicit mobile number/count/throttle, browser-like headers, repeated requests and blocking sleep. fileciteturn378file0L2-L6

| Mechanism | load2 mapping | Decision |
|---|---|---|
| count | bounded scenario | ADAPT |
| throttle | centralized actual-operation pacing | ADAPT |
| request headers | transport metadata | REFERENCE |
| concrete public endpoint | public-target flooding | REJECT |
| broad exception | structured failure classification | HARDEN |

No new mechanism beyond the first SMS implementation is required from this file.

## 7. `numspy_bomber.py`

The source uses a `Way2sms` client, logs in with supplied credentials, repeats `send(mobile_number,message)` for a requested count and logs out. It also states a service-side daily limit in its prompt. fileciteturn381file0L2-L6

| Mechanism | load2 mapping | Decision |
|---|---|---|
| third-party client abstraction | provider adapter concept | EXTRACT |
| explicit finite count | bounded scenario | ADAPT |
| login/logout lifecycle | session lifecycle abstraction | REFERENCE |
| service limit awareness | rate-limit detection/reporting | ADOPT concept |
| direct third-party SMS delivery | outside current SMTP transport scope | REJECT for direct import |
| interactive credentials | secret-provider model instead | REJECT pattern |

The important load2 lesson is that provider limits should be surfaced as observable outcomes rather than blindly retried.

## 8. `Twitter_bomber.py`

The source automates a browser with Selenium, accepts account credentials, navigates to login and a target profile, opens messaging, then sends a selected message a requested number of times. The source defines both manual text and file-selection paths, although the shown execution calls `bombMsg(instances, Content)`. fileciteturn379file0L2-L6

| Mechanism | load2 mapping | Decision |
|---|---|---|
| browser/session lifecycle | generic transport/session abstraction | REFERENCE |
| finite message count | bounded scenario | ADAPT |
| payload from manual/file source | payload abstraction | ADAPT |
| explicit target identifier | target model | REFERENCE |
| authenticated session | session lifecycle concept | REFERENCE |
| third-party UI automation | outside SMTP network layer | REFERENCE |
| account credentials in CLI | secret-provider approach instead | REJECT pattern |

No Selenium/browser automation should be introduced into load2 merely because this repository uses it.

## 9. `wbomb.py`

The source starts a Selenium Chrome session, opens WhatsApp Web, waits for QR authentication, asks for a user/group name, message and count, selects the matching conversation and sends the message repeatedly. fileciteturn380file0L2-L6

| Mechanism | load2 mapping | Decision |
|---|---|---|
| persistent browser session | generic session lifecycle reference | REFERENCE |
| explicit target + payload + count | scenario model | ADAPT concept |
| repeated submission | bounded repeated-operation scenario | SIMULATE concept |
| QR/user-session dependency | external UI authentication model | REFERENCE |
| WhatsApp UI automation | outside SMTP transport layer | REFERENCE |

No WhatsApp automation is imported.

## 10. Cross-repository mechanism inventory

| Mechanism | Evidence | load2 decision |
|---|---|---|
| finite count | email/SMS/spam/browser modules | ADAPT/ADOPT |
| duration/deadline scenario | bomber-family execution pattern | ADAPT |
| explicit interval/throttle | email/SMS/spam modules | ADAPT |
| target set from file | `Email_Bomber_Version2.py`, `spam.py` | ADAPT |
| payload source from file | `spam.py`, `Twitter_bomber.py` | ADAPT via plugin |
| persistent SMTP session | `Email_bomber.py` | ADOPT/HARDEN |
| connection-per-message benchmark | `Email_Bomber_Version2.py` | SIMULATE |
| provider/endpoint registry | provider branches + SMS endpoint lists | ADAPT |
| endpoint health/rate-limit awareness | service-limit/provider patterns | ADOPT concept |
| bounded fan-out | useful abstraction from multi-endpoint execution | ADAPT |
| structured outcomes | source counters/exceptions show the need | HARDEN |
| session lifecycle | SMTP/browser/provider modules | ADOPT concept |
| infinite repetition | `spam.py` | SIMULATE only with cancellation/hard limits |
| arbitrary public-target bombing | multiple modules | REJECT |
| verification bypass / temporary-number abuse | README inventory | REJECT |
| credential-in-input patterns | email/Twitter/SMS modules | REJECT |

## 11. What this changes in the load2 backlog

This audit does **not** add a spam/stress implementation. It adds inventory-backed architecture candidates:

1. explicit `Scenario` model: target scope + payload + count/duration + rate + concurrency + transport + cancellation;
2. target-set source abstraction with canonicalization, deduplication and scope validation;
3. payload source/plugin abstraction for deterministic file-backed test data;
4. provider/endpoint registry with health and quarantine state;
5. persistent-session vs bounded connection-churn benchmark mode;
6. formal failure taxonomy including provider rate-limit outcomes;
7. replayable scenario metadata without credentials/secrets;
8. transport/session lifecycle metrics.

Implementation remains a separate step after the complete external-repository inventory is finished.

## 12. Required tests for any later implementation

- finite count produces exactly N logical attempts;
- duration/deadline terminates promptly;
- target lists are canonicalized/deduplicated and scope-validated;
- file payloads are deterministic when requested;
- global pacing is enforced at actual operation/send time;
- connection-churn tests remain bounded;
- provider/endpoint health can quarantine failed endpoints;
- rate-limit outcomes are classified without uncontrolled retry loops;
- cancellation releases workers, limiters, adaptive concurrency, pools and send gate;
- delivery ledger prevents duplicate logical deliveries;
- secrets are excluded from logs and replay artifacts.

## Conclusion

`bhattsameer/Bombers` is now audited at the execution-module level rather than only from its README. The highest-value finding for `load2` is the **persistent SMTP session + explicit count/rate scenario model**, supplemented by target-set abstraction, provider/endpoint abstraction, rate-limit observability and controlled connection-churn benchmarking.

The SMS/browser/GUI bomber implementations provide transport and orchestration patterns but do not justify importing their concrete public-target delivery paths. No spam/stress module has been added to `load2` during this audit.
