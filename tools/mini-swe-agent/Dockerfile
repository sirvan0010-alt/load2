FROM python:3.12-slim

ARG MINI_SWE_AGENT_REF=04d809ceab9df28f9adaed044884180159172930

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && python -m pip install --no-cache-dir \
        "git+https://github.com/SWE-agent/mini-swe-agent.git@${MINI_SWE_AGENT_REF}" \
    && apt-get purge -y --auto-remove git \
    && rm -rf /root/.cache

WORKDIR /workspace

ENTRYPOINT ["mini-swe-agent"]
