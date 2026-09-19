#!/usr/bin/env bash
set -euo pipefail

RESULT_DIR="${1:-swarm-results}"
EXPECTED_AGENTS=(
  ORCHESTRATOR RESEARCH_AGENT BUG_TRIAGE_AGENT FEATURE_ARCHITECT_AGENT
  IMPLEMENTATION_AGENT REFACTOR_AGENT INTEGRATION_AGENT SECURITY_AGENT
  EVIDENCE_AGENT REVIEW_AGENT TEST_AGENT DOCUMENTATION_AGENT RELEASE_AGENT
)

if [[ ! -d "$RESULT_DIR" ]]; then
  echo "Agent result directory missing: $RESULT_DIR" >&2
  exit 2
fi

failures=0
for agent in "${EXPECTED_AGENTS[@]}"; do
  file="$RESULT_DIR/${agent}.json"
  if [[ ! -f "$file" ]]; then
    echo "MISSING: $file" >&2
    failures=$((failures + 1))
    continue
  fi

  if ! jq -e --arg agent "$agent" '
      .schema_version == "1.0" and
      .repository == "sirvan0010-alt/load2" and
      .agent == $agent and
      (.commit | type == "string" and test("^[0-9a-f]{40}$")) and
      (.status == "READY" or .status == "BLOCKED" or .status == "NEEDS-EVIDENCE") and
      (.findings | type == "array") and
      (.acceptance | type == "array") and
      (.verification.tests | type == "array") and
      (.security.authorization == "PASS" or .security.authorization == "FAIL" or .security.authorization == "NOT-APPLICABLE") and
      (.security.cancellation == "PASS" or .security.cancellation == "FAIL" or .security.cancellation == "NOT-APPLICABLE") and
      (.security.hard_limits == "PASS" or .security.hard_limits == "FAIL" or .security.hard_limits == "NOT-APPLICABLE") and
      (.security.secrets == "PASS" or .security.secrets == "FAIL" or .security.secrets == "NOT-APPLICABLE") and
      (.handoff | type == "string" and length > 0)
    ' "$file" >/dev/null; then
    echo "INVALID: $file" >&2
    failures=$((failures + 1))
    continue
  fi

  status="$(jq -r '.status' "$file")"
  if [[ "$status" == "READY" ]]; then
    if ! jq -e '
        (.findings | length > 0) and
        (.acceptance | length > 0) and
        (.verification.tests | length > 0) and
        ([.acceptance[].result] | any(. == "PASS")) and
        (.security | [.authorization, .cancellation, .hard_limits, .secrets] | all(. == "PASS" or . == "NOT-APPLICABLE"))
      ' "$file" >/dev/null; then
      echo "UNVERIFIED READY: $file" >&2
      failures=$((failures + 1))
    fi
  fi

done

if (( failures > 0 )); then
  echo "Agent verification gate FAILED: $failures result(s)." >&2
  exit 1
fi

echo "Agent verification gate PASSED: ${#EXPECTED_AGENTS[@]} results verified."
