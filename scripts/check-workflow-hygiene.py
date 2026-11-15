#!/usr/bin/env python3
"""Fail CI on unsafe or under-scoped GitHub Actions workflow definitions."""

from pathlib import Path
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
WORKFLOWS = ROOT / ".github" / "workflows"

errors: list[str] = []
files = sorted(
    path for path in WORKFLOWS.iterdir()
    if path.is_file() and path.suffix in {".yml", ".yaml"}
)

if not files:
    errors.append("No GitHub Actions workflow files were found.")

# Parse every workflow with Ruby's standard Psych YAML parser. GitHub-hosted
# Ubuntu runners include Ruby; parsing catches malformed YAML that text checks
# alone cannot detect. Parse only (do not evaluate) workflow expressions.
ruby = shutil.which("ruby")
if ruby:
    parser = (
        "require 'yaml'; "
        "ARGV.each { |file| YAML.parse_file(file); puts \"YAML OK: #{file}\" }"
    )
    parsed = subprocess.run(
        [ruby, "-e", parser, *[str(path) for path in files]],
        cwd=ROOT,
        text=True,
        capture_output=True,
        check=False,
    )
    if parsed.returncode:
        errors.append("Workflow YAML syntax validation failed:\\n" + parsed.stderr.strip())
else:
    errors.append("Ruby/Psych is unavailable; workflow YAML syntax could not be validated.")

for path in files:
    content = path.read_text(encoding="utf-8")
    relative = path.relative_to(ROOT)

    if not re.search(r"(?m)^\s*permissions:\s*$", content):
        errors.append(f"{relative}: missing explicit GITHUB_TOKEN permissions.")

    if re.search(r"(?m)^\s*permissions:\s*write-all\s*$", content):
        errors.append(f"{relative}: permissions: write-all is not allowed.")

    if re.search(r"(?m)^\s*(pull_request_target|workflow_run):", content):
        errors.append(f"{relative}: sensitive trigger requires a separate security review.")

    if re.search(r"curl\s+[^\n]*\|\s*(bash|sh)|wget\s+[^\n]*\|\s*(bash|sh)", content):
        errors.append(f"{relative}: remote script execution through a shell pipe is not allowed.")

    for match in re.finditer(r"(?m)^\s*uses:\s*([^\s#]+)", content):
        action = match.group(1)
        if re.search(r"@(main|master|HEAD)$", action, re.IGNORECASE):
            errors.append(f"{relative}: action reference is not version-pinned: {action}")

if errors:
    print("\n".join(errors))
    print(f"Workflow hygiene failed ({len(errors)} issue(s), {len(files)} workflow(s) checked).")
    sys.exit(1)

print(f"Workflow hygiene passed ({len(files)} workflow(s): explicit permissions, no write-all, unsafe triggers, remote shell pipes or floating branch refs).")
