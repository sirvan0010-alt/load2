#!/usr/bin/env bash
set -euo pipefail

fail=0
require_file() {
  if [[ ! -f "$1" ]]; then
    echo "MISSING: $1" >&2
    fail=1
  else
    echo "OK: $1"
  fi
}

require_text() {
  local file="$1"
  local text="$2"
  if ! grep -Fq "$text" "$file"; then
    echo "MISSING EVIDENCE: $file -> $text" >&2
    fail=1
  else
    echo "OK: $file contains required evidence marker"
  fi
}

require_file "src/MailLoadTester.Core/AutonomousAgentContracts.cs"
require_file "src/MailLoadTester.Core/OpenHandsAgentExecutionBackend.cs"
require_file "src/MailLoadTester.Core/AutonomousAgentOrchestrator.cs"
require_file "tests/MailLoadTester.Tests/AutonomousAgentRepairLoopTests.cs"
require_file ".github/workflows/autonomous-pr-gate.yml"
require_file ".github/workflows/autonomous-maintenance.yml"
require_file "scripts/verify-agent-results.sh"

require_text "src/MailLoadTester.Core/AutonomousAgentContracts.cs" "request.TimeBudget"
require_text "src/MailLoadTester.Core/AutonomousAgentContracts.cs" "request.MaxIterations"
require_text "src/MailLoadTester.Core/AutonomousAgentContracts.cs" "RepairFeedback"
require_text ".github/workflows/autonomous-pr-gate.yml" "--base main"
require_text ".github/workflows/autonomous-pr-gate.yml" "never merge"
require_text ".github/workflows/autonomous-maintenance.yml" "AUTONOMOUS_MAINTENANCE_ENABLED"

# A5 is a hard prerequisite for A10. Evidence must come from the OpenHands
# event stream, not from the final natural-language response.
require_text "src/MailLoadTester.Core/OpenHandsAgentExecutionBackend.cs" "/events/search"

if grep -RIn --exclude-dir=.git --exclude='*.md' "gh pr merge" .github scripts src tests >/dev/null 2>&1; then
  echo "FORBIDDEN: automatic protected-branch merge path detected" >&2
  fail=1
else
  echo "OK: no automatic merge command detected"
fi

if (( fail != 0 )); then
  echo "A10 BLOCKED: autonomous swarm prerequisites are not all verified." >&2
  exit 1
fi

echo "A10 PASS: all autonomous swarm prerequisites are present and evidence-backed."
