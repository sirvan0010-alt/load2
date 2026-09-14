# AI Agent Architecture

This repository uses specialized verification roles rather than one generic AI reviewer. Each role has a narrow responsibility and must report evidence, findings, and confidence separately.

## Agents

### 1. Build / CI Agent
- Runs the canonical .NET restore, build, and full test suite.
- Never declares the repository healthy from static inspection alone.
- A green build is a build result, not proof of runtime correctness.

### 2. CodeQL Security Agent
- Uses GitHub CodeQL Advanced analysis for C# and GitHub Actions workflows.
- Treats security findings as independent from functional test results.
- Does not suppress findings merely to make CI green.

### 3. Dependency / Actions Hygiene Agent
- Reviews dependency risk, vulnerable transitive packages, and GitHub Actions supply-chain changes.
- Favors maintained actions and least-privilege workflow permissions.
- Dependency warnings remain visible until consciously triaged.

### 4. Architecture Consistency Agent
- Checks that source/test/project boundaries remain intact.
- Detects accidental duplication, generated/build output committed to source control, and changes that bypass the established Core/Test architecture.

### 5. Test / Regression Agent
- Maps changed production code to relevant tests.
- Requires regression coverage for bug fixes where practical.
- Distinguishes existing coverage from newly added proof.

### 6. Evidence / Audit Agent
- Separates facts, observed repository state, test evidence, external documentation, and AI inference.
- Never upgrades an inference into a fact.
- Audit documents must identify what was actually checked.

### 7. Release Gate Agent
- Final verifier before release/merge decisions.
- Checks CI, security, dependency, architecture, tests, and documentation state.
- Reports blockers explicitly; never hides a failed gate.

## Coordination order

`CHANGE -> BUILD/TEST -> SECURITY -> DEPENDENCIES/ACTIONS -> ARCHITECTURE -> EVIDENCE/AUDIT -> RELEASE GATE`

## Hard rules

1. Green CI does not mean secure.
2. CodeQL clean does not mean functionally correct.
3. Passing tests do not prove production behavior outside the tested boundary.
4. AI inference must be labeled as inference.
5. A failed security or regression gate must remain visible.
6. Do not make unrelated refactors while fixing a concrete issue.
7. Prefer small, reviewable commits and preserve the repository's source-of-truth documents.
