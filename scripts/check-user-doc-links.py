#!/usr/bin/env python3
"""Check relative links in the current user-facing Markdown documentation."""

from pathlib import Path
import re
import sys
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
FILES = (
    "README.md",
    "README-BUILD.md",
    "docs/README.md",
    "docs/USER_GUIDE.md",
    "docs/RESULTS_AND_METRICS.md",
    "docs/SAFETY_AND_PRIVACY.md",
    "docs/TROUBLESHOOTING.md",
)
LINK = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")

errors: list[str] = []
checked = 0

for relative_file in FILES:
    source = ROOT / relative_file
    if not source.is_file():
        errors.append(f"{relative_file}: documentation file is missing")
        continue

    content = source.read_text(encoding="utf-8")
    for match in LINK.finditer(content):
        raw_target = match.group(1).strip()
        if raw_target.startswith("<") and ">" in raw_target:
            raw_target = raw_target[1:raw_target.index(">")]
        else:
            raw_target = raw_target.split(maxsplit=1)[0]

        parsed = urlsplit(raw_target)
        if parsed.scheme or parsed.netloc or not parsed.path:
            continue

        target_path = unquote(parsed.path)
        candidate = (
            ROOT / target_path.lstrip("/")
            if target_path.startswith("/")
            else source.parent / target_path
        )
        candidate = candidate.resolve()
        checked += 1

        if not candidate.is_relative_to(ROOT):
            errors.append(f"{relative_file}: link escapes repository root: {raw_target}")
        elif not candidate.exists():
            errors.append(f"{relative_file}: broken relative link: {raw_target}")

if errors:
    print("\n".join(errors))
    print(f"Documentation link check failed ({len(errors)} issue(s), {checked} relative link(s) checked).")
    sys.exit(1)

print(f"Documentation link check passed ({checked} relative link(s) checked).")
