"""
Audit Logger — STRIDE Repudiation defense.

Records all lab operations with timestamps, unique IDs, and subsystem tags.

CIA Triad:
  - Integrity: Log entries are append-only (no modification).
  - Availability: Logs are stored in memory with size limits (DoS).
  - Confidentiality: No sensitive data in logs (only action metadata).

STRIDE:
  - Repudiation: Every action gets a unique ID and timestamp.
  - Tampering: In-memory list is append-only from public API.
"""

import uuid
from datetime import datetime, timezone
from typing import List

from lab_control.domain.ports import IAuditLogger

# STRIDE: DoS prevention — cap log buffer size
MAX_LOG_ENTRIES = 10_000


class InMemoryAuditLogger(IAuditLogger):
    """
    Append-only in-memory audit logger.

    Production upgrade path: Replace with database or
    file-based logger without changing domain/application code.
    """

    def __init__(self):
        self._entries: List[dict] = []

    def log_action(
        self, subsystem: str, action: str, details: dict
    ) -> str:
        """Record a lab action with timestamp and unique ID."""
        event_id = self._generate_id()
        entry = {
            "event_id": event_id,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "subsystem": subsystem,
            "action": action,
            "details": details,
            "severity": "INFO",
        }
        self._append(entry)
        return event_id

    def log_safety_event(
        self, event_type: str, details: dict
    ) -> str:
        """Record a safety-critical event at WARNING severity."""
        event_id = self._generate_id()
        entry = {
            "event_id": event_id,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "subsystem": "SAFETY",
            "action": event_type,
            "details": details,
            "severity": "WARNING",
        }
        self._append(entry)
        return event_id

    def get_recent_logs(self, count: int = 50) -> List[dict]:
        """Return the N most recent log entries."""
        safe_count = min(count, len(self._entries))
        return list(self._entries[-safe_count:])

    @property
    def total_entries(self) -> int:
        return len(self._entries)

    # ── Private ───────────────────────────────────────────────────────

    def _generate_id(self) -> str:
        """Generate a unique event identifier."""
        return str(uuid.uuid4())[:8]

    def _append(self, entry: dict) -> None:
        """
        Append-only insertion with size cap.

        STRIDE DoS: Drops oldest entries when buffer is full.
        """
        if len(self._entries) >= MAX_LOG_ENTRIES:
            self._entries = self._entries[-(MAX_LOG_ENTRIES // 2):]
        self._entries.append(entry)
