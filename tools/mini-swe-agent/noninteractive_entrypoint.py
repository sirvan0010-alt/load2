#!/usr/bin/env python3
"""Non-interactive entrypoint for load2 Docker mini-SWE-agent smoke runs.

The stock `mini` / `mini-swe-agent` CLI (minisweagent.run.mini) always imports
prompt_toolkit-backed helpers and calls configure_if_first_time(). In CI/Docker
without a TTY that path prints:

    Warning: Input is not a terminal (fd=0). Aborted.

even when -y / --exit-immediately / MSWEA_CONFIGURED are attempted around the
interactive agent.

This entrypoint never imports minisweagent.run.mini or prompt_user. It uses the
Python API: DefaultAgent + configured model/environment only.
"""
from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

# Must be set before importing minisweagent (dotenv load / first-time gates).
os.environ["MSWEA_CONFIGURED"] = "true"
os.environ["MSWEA_SILENT_STARTUP"] = "1"

# Stable contract marker asserted by load2 Docker smoke tests.
SMOKE_OK_MARKER = "LOAD2_MINI_SWE_SMOKE_OK"


def _prepare_global_config() -> None:
    """Ensure global config dir/.env exist on writable tmpfs paths."""
    from platformdirs import user_config_dir

    config_dir = Path(os.getenv("MSWEA_GLOBAL_CONFIG_DIR") or user_config_dir("mini-swe-agent"))
    config_dir.mkdir(parents=True, exist_ok=True)
    env_file = config_dir / ".env"
    if not env_file.exists():
        env_file.write_text("MSWEA_CONFIGURED=true\n", encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="load2 non-interactive mini-SWE-agent runner")
    parser.add_argument("--task", required=True, help="Task prompt (never prompted interactively)")
    parser.add_argument("--config", default=None, help="Optional YAML config path")
    parser.add_argument("--model", default=None, help="Optional model name override")
    args = parser.parse_args(argv)

    _prepare_global_config()

    # Import only non-interactive API surface.
    from minisweagent.agents.default import DefaultAgent
    from minisweagent.config import get_config_from_spec
    from minisweagent.environments import get_environment
    from minisweagent.models import get_model
    from minisweagent.utils.serialize import recursive_merge

    configs: list[dict] = []
    if args.config:
        configs.append(get_config_from_spec(args.config))

    model_cfg: dict = {}
    agent_cfg: dict = {}
    env_cfg: dict = {}
    if configs:
        merged = recursive_merge(*configs)
        model_cfg = dict(merged.get("model") or {})
        agent_cfg = dict(merged.get("agent") or {})
        env_cfg = dict(merged.get("environment") or {})

    if args.model:
        model_cfg["model_name"] = args.model

    # Strip interactive-only keys if present in a shared yaml.
    agent_cfg.pop("mode", None)
    agent_cfg.pop("confirm_exit", None)
    agent_cfg.pop("agent_class", None)

    model = get_model(config=model_cfg)
    env = get_environment(env_cfg, default_type="local")
    agent = DefaultAgent(model, env, **agent_cfg)
    result = agent.run(args.task)

    # Always print the structured result for diagnostics.
    print(result)

    exit_status = None
    if isinstance(result, dict):
        exit_status = result.get("exit_status")
        submission = result.get("submission") or ""
        if submission:
            print(submission)

    # Success contract for load2 boundary smoke: DefaultAgent finished without TTY.
    if exit_status in (None, "Submitted", "LimitsExceeded"):
        # LimitsExceeded can still prove the runner executed; only hard-fail on exceptions.
        if exit_status == "Submitted" or exit_status is None:
            print(SMOKE_OK_MARKER)
            print(f"exit_status={exit_status}")
            return 0
        print(f"exit_status={exit_status}")
        print(SMOKE_OK_MARKER)
        return 0

    print(f"unexpected exit_status={exit_status}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:  # noqa: BLE001 - boundary must always exit non-interactively
        print(f"noninteractive_entrypoint failure: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
