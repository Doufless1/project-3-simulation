"""
Audit Logger — Simulation run logging for STRIDE compliance.

STRIDE Mitigation (Repudiation):
- Records all simulation starts, completions, and errors.
- Each run gets a unique ID for correlation.
- Timestamps in ISO 8601 format.

Single Responsibility: Only handles audit log I/O.
"""

import hashlib
import json
import logging
import os
import uuid
from datetime import datetime, timezone


from domain.ports import IAuditLogger


class AuditLogger(IAuditLogger):
    """
    File-based audit logger for simulation runs.

    Logs to 'simulation_audit.log' with structured JSON entries.
    """

    def __init__(self, log_dir: str = "."):
        self._log_path = os.path.join(log_dir, "simulation_audit.log")

        # Configure Python logger
        self._logger = logging.getLogger("simulation_audit")
        self._logger.setLevel(logging.INFO)

        # Avoid duplicate handlers on re-init
        if not self._logger.handlers:
            handler = logging.FileHandler(self._log_path, encoding="utf-8")
            handler.setFormatter(
                logging.Formatter("%(message)s")
            )
            self._logger.addHandler(handler)

    def log_simulation_start(
        self, material_name: str, solver_name: str, params: dict
    ) -> str:
        """Log simulation start. Returns a unique run ID."""
        run_id = str(uuid.uuid4())[:8]
        entry = {
            "event": "SIMULATION_START",
            "run_id": run_id,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "material": material_name,
            "solver": solver_name,
            "params": self._sanitize_params(params),
        }
        self._logger.info(json.dumps(entry))
        return run_id

    def log_simulation_end(
        self, run_id: str, result_summary: dict
    ) -> None:
        """Log simulation completion with result hash for integrity."""
        result_hash = hashlib.sha256(
            json.dumps(result_summary, sort_keys=True).encode()
        ).hexdigest()[:16]

        entry = {
            "event": "SIMULATION_END",
            "run_id": run_id,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "result_hash": result_hash,
            "summary": result_summary,
        }
        self._logger.info(json.dumps(entry))

    def log_error(self, run_id: str, error: str) -> None:
        """Log simulation error."""
        entry = {
            "event": "SIMULATION_ERROR",
            "run_id": run_id,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "error": error,
        }
        self._logger.info(json.dumps(entry))

    @staticmethod
    def _sanitize_params(params: dict) -> dict:
        """Remove sensitive values before logging (CIA: Confidentiality)."""
        sanitized = {}
        sensitive_keys = {"api_key", "password", "secret", "token"}
        for key, value in params.items():
            if key.lower() in sensitive_keys:
                sanitized[key] = "***REDACTED***"
            else:
                sanitized[key] = value
        return sanitized
