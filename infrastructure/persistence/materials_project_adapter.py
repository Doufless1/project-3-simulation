"""
Materials Project API Adapter — External data source.

Implements IMaterialRepository for the Materials Project REST API.

CIA Triad:
- Confidentiality: API key loaded from environment variable only.
- Availability: Falls back gracefully if API is unreachable.

STRIDE:
- Spoofing: API key validated before requests.
- Information Disclosure: Key never logged or stored in source.
"""

import os
from typing import List

from domain.ports import IMaterialRepository


class MaterialsProjectAdapter(IMaterialRepository):
    """
    Repository: Fetches material data from the Materials Project API.

    Requires MP_API_KEY environment variable.
    Falls back to empty results if unavailable (Availability).
    """

    def __init__(self):
        self._api_key = os.environ.get("MP_API_KEY", "")
        self._available = bool(self._api_key)

        if self._available:
            try:
                from mp_api.client import MPRester
                self._rester = MPRester(self._api_key)
            except ImportError:
                self._available = False
                self._rester = None
        else:
            self._rester = None

    @property
    def is_available(self) -> bool:
        """Check if the API connection is active."""
        return self._available

    def save(self, material_data: dict) -> None:
        """Not supported — API is read-only."""
        raise NotImplementedError(
            "MaterialsProjectAdapter is read-only."
        )

    def load_all(self) -> List[dict]:
        """Fetch all available materials (limited to first 100)."""
        if not self._available:
            return []

        try:
            docs = self._rester.summary.search(
                fields=["material_id", "formula_pretty", "density"],
                num_chunks=1,
            )
            return [
                {
                    "id": str(d.material_id),
                    "formula": d.formula_pretty,
                    "density": d.density,
                }
                for d in docs[:100]
            ]
        except Exception:
            return []

    def find_best(self, n: int = 5) -> List[dict]:
        """Not applicable for raw API data — returns first N."""
        return self.load_all()[:n]
