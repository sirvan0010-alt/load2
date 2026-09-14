# AI Agent Registry

| Agent | Primary question | Gate |
|---|---|---|
| Build / CI | Does the repository restore, build and test? | Required |
| CodeQL Security | Are supported code/workflow security checks clean? | Required |
| Dependency / Actions Hygiene | Did dependencies or workflows introduce avoidable risk? | Required when relevant |
| Architecture Consistency | Is the repository structure still coherent? | Required |
| Test / Regression | Is changed behavior covered by tests? | Required for bug fixes where practical |
| Evidence / Audit | What is actually proven versus inferred? | Required for audit-sensitive changes |
| Release Gate | Are all required gates satisfied? | Final |

## Status vocabulary

`PASS` = checked and passed.
`FAIL` = checked and failed.
`BLOCKED` = cannot be evaluated because required input is unavailable.
`NOT_APPLICABLE` = deliberately outside the scope of the change.

A missing check is never silently treated as PASS.
