#!/usr/bin/env python3
"""Non-interactive entrypoint for load2 Docker mini-SWE-agent smoke runs.

Never imports minisweagent.run.mini / prompt_user (TTY abort path).
Uses DefaultAgent + configured model/environment only.
"""
from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

os.environ["MSWEA_CONFIGURED"] = "true"
os.environ["MSWEA_SILENT_STARTUP"] = "1"

SMOKE_OK_MARKER = "LOAD2_MINI_SWE_SMOKE_OK"


def _prepare_global_config() -> None:
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

    agent_cfg.pop("mode", None)
    agent_cfg.pop("confirm_exit", None)
    agent_cfg.pop("agent_class", None)

    # Ensure deterministic test models have enough scripted replies for a full step.
    # DefaultAgent may query more than once (action → observation → finish).
    outputs = model_cfg.get("outputs")
    if isinstance(outputs, list) and len(outputs) == 1:
        model_cfg["outputs"] = [outputs[0], outputs[0]]

    model = get_model(config=model_cfg)
    env = get_environment(env_cfg, default_type="local")
    agent = DefaultAgent(model, env, **agent_cfg)

    try:
        result = agent.run(args.task)
    except IndexError as exc:
        # DeterministicModel exhausted scripted outputs mid-run.
        print(f"noninteractive_entrypoint failure: {exc}", file=sys.stderr)
        print("hint: model.outputs needs one entry per DefaultAgent query step", file=sys.stderr)
        return 1

    print(result)

    exit_status = None
    if isinstance(result, dict):
        exit_status = result.get("exit_status")
        submission = result.get("submission") or ""
        if submission:
            print(submission)

    print(f"exit_status={exit_status}")
    # Boundary smoke success: agent ran to a terminal status without TTY.
    if exit_status in ("Submitted", "LimitsExceeded", None):
        print(SMOKE_OK_MARKER)
        return 0

    print(f"unexpected exit_status={exit_status}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:  # noqa: BLE001
        print(f"noninteractive_entrypoint failure: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
