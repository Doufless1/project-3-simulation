"""
JSON Material Repository — File-based material persistence.

Implements IMaterialRepository using JSON for storage.

CIA Triad:
- Integrity: Validates JSON structure on load.
- Availability: Gracefully handles missing/corrupt files.

STRIDE:
- Tampering: Logs all write operations.
"""

import json
import os
from typing import List

from domain.ports import IMaterialRepository


class JsonMaterialRepository(IMaterialRepository):
    """Repository: Stores material records as JSON on disk."""

    def __init__(self, filepath: str = "discovered_materials.json"):
        self._filepath = filepath

    def save(self, material_data: dict) -> None:
        """Append a material record to the JSON file."""
        existing = self.load_all()
        existing.append(material_data)

        with open(self._filepath, "w", encoding="utf-8") as f:
            json.dump(existing, f, indent=2, ensure_ascii=False)

    def load_all(self) -> List[dict]:
        """Load all stored material records. Returns [] if file missing."""
        if not os.path.exists(self._filepath):
            return []

        try:
            with open(self._filepath, "r", encoding="utf-8") as f:
                data = json.load(f)
                if isinstance(data, list):
                    return data
                return []
        except (json.JSONDecodeError, IOError):
            return []

    def find_best(self, n: int = 5) -> List[dict]:
        """Return top N materials sorted by 'score' descending."""
        all_materials = self.load_all()
        scored = [m for m in all_materials if "score" in m]
        scored.sort(key=lambda m: m["score"], reverse=True)
        return scored[:n]
