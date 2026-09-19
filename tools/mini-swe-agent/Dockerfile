FROM python:3.12-slim

ARG MINI_SWE_AGENT_REF=04d809ceab9df28f9adaed044884180159172930

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    MSWEA_CONFIGURED=true \
    MSWEA_SILENT_STARTUP=1 \
    MSWEA_GLOBAL_CONFIG_DIR=/tmp/mini-swe-agent-config

RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && python -m pip install --no-cache-dir \
        "git+https://github.com/SWE-agent/mini-swe-agent.git@${MINI_SWE_AGENT_REF}" \
    && apt-get purge -y --auto-remove git \
    && rm -rf /root/.cache \
    && mkdir -p /tmp/mini-swe-agent-config \
    && printf 'MSWEA_CONFIGURED=true\n' > /tmp/mini-swe-agent-config/.env

COPY noninteractive_entrypoint.py /usr/local/bin/load2-mini-swe-noninteractive
RUN chmod 0755 /usr/local/bin/load2-mini-swe-noninteractive

WORKDIR /workspace
ENTRYPOINT ["python", "/usr/local/bin/load2-mini-swe-noninteractive"]
