"""
Materials Project API Integration

REAL database with 150,000+ materials from materialsproject.org

SETUP:
1. Register at https://materialsproject.org/
2. Get your API key from your profile
3. Set environment variable: MP_API_KEY=your_key_here
   Or pass it directly to MaterialsProjectDB(api_key="...")

USAGE:
    from materials_project_db import MaterialsProjectDB
    db = MaterialsProjectDB()
    materials = db.search_high_temp_alloys(limit=100)
"""

import logging
import os
from typing import List, Dict, Optional
from dataclasses import dataclass

# Check if mp_api is available
try:
    from mp_api.client import MPRester
    MP_API_AVAILABLE = True
except ImportError:
    MP_API_AVAILABLE = False
    logging.getLogger(__name__).warning("mp-api not installed. Run: pip install mp-api pymatgen")

@dataclass
class MaterialEntry:
    """A material from the Materials Project database."""
    material_id: str
    formula: str
    elements: List[str]
    
    # Properties (may be None if not available)
    melting_point: Optional[float] = None  # Kelvin
    density: Optional[float] = None        # g/cm³ -> kg/m³
    formation_energy: Optional[float] = None  # eV/atom
    band_gap: Optional[float] = None       # eV
    is_stable: bool = False
    
    # Derived
    melting_point_C: Optional[float] = None
    
    def __post_init__(self):
        if self.melting_point:
            self.melting_point_C = self.melting_point - 273.15


class MaterialsProjectDB:
    """
    Interface to the Materials Project database.
    
    Contains 150,000+ computationally verified materials!
    """
    
    def __init__(self, api_key: str = None):
        """
        Initialize with API key.
        
        Get your free key at: https://materialsproject.org/
        """
        self.api_key = api_key or os.getenv("MP_API_KEY", "")
        
        if not self.api_key:
            raise ValueError(
                "Materials Project API key required!\n"
                "1. Register at https://materialsproject.org/\n"
                "2. Get your API key from your profile\n"
                "3. Set: MP_API_KEY=your_key OR pass api_key='...'"
            )
        
        if not MP_API_AVAILABLE:
            raise ImportError("mp-api not installed. Run: pip install mp-api pymatgen")
        
        self.client = MPRester(self.api_key)
        print(f"Connected to Materials Project API")
    
    def get_material_count(self) -> int:
        """Get total number of materials in database."""
        # This is an approximation
        return 154000  # As of 2024
    
    def search_by_elements(self, elements: List[str], limit: int = 100) -> List[MaterialEntry]:
        """
        Search for materials containing specific elements.
        
        Example: search_by_elements(['W', 'C']) -> Tungsten Carbides
        """
        docs = self.client.materials.summary.search(
            elements=elements,
            fields=["material_id", "formula_pretty", "elements", "density", 
                    "formation_energy_per_atom", "band_gap", "is_stable"],
            num_chunks=1,
            chunk_size=limit
        )
        
        return [self._convert_doc(doc) for doc in docs]
    
    def search_high_temp_alloys(self, limit: int = 100) -> List[MaterialEntry]:
        """
        Search for high-temperature resistant materials.
        Focuses on refractory metals: W, Mo, Ta, Nb, Re
        """
        refractory_elements = ['W', 'Mo', 'Ta', 'Nb']
        all_materials = []
        
        for elem in refractory_elements:
            docs = self.client.materials.summary.search(
                elements=[elem],
                is_stable=True,
                fields=["material_id", "formula_pretty", "elements", "density",
                        "formation_energy_per_atom", "band_gap", "is_stable"],
                num_chunks=1,
                chunk_size=limit // len(refractory_elements)
            )
            all_materials.extend([self._convert_doc(doc) for doc in docs])
        
        return all_materials
    
    def search_carbides(self, limit: int = 50) -> List[MaterialEntry]:
        """Search for carbide materials (extremely hard)."""
        docs = self.client.materials.summary.search(
            elements=['C'],
            is_stable=True,
            fields=["material_id", "formula_pretty", "elements", "density",
                    "formation_energy_per_atom", "band_gap", "is_stable"],
            num_chunks=1,
            chunk_size=limit
        )
        
        # Filter to only include metal carbides
        carbides = []
        metals = {'W', 'Ti', 'Mo', 'Cr', 'Ta', 'Nb', 'V', 'Zr', 'Hf'}
        for doc in docs:
            if any(m in doc.elements for m in metals):
                carbides.append(self._convert_doc(doc))
        
        return carbides
    
    def get_all_tungsten_compounds(self, limit: int = 500) -> List[MaterialEntry]:
        """Get all tungsten compounds (best for high-temp)."""
        docs = self.client.materials.summary.search(
            elements=['W'],
            fields=["material_id", "formula_pretty", "elements", "density",
                    "formation_energy_per_atom", "band_gap", "is_stable"],
            num_chunks=1,
            chunk_size=limit
        )
        return [self._convert_doc(doc) for doc in docs]
    
    def _convert_doc(self, doc) -> MaterialEntry:
        """Convert API response to MaterialEntry."""
        return MaterialEntry(
            material_id=str(doc.material_id),
            formula=doc.formula_pretty,
            elements=list(doc.elements),
            density=doc.density * 1000 if doc.density else None,  # g/cm³ -> kg/m³
            formation_energy=doc.formation_energy_per_atom,
            band_gap=doc.band_gap,
            is_stable=doc.is_stable
        )


# ============================================================================
# OFFLINE FALLBACK: If no API key, use a small curated dataset
# ============================================================================
OFFLINE_HIGH_TEMP_MATERIALS = [
    {"id": "mp-91", "formula": "W", "T_melt_C": 3422, "density": 19250, "hardness_HV": 3500},
    {"id": "mp-1094", "formula": "WC", "T_melt_C": 2870, "density": 15630, "hardness_HV": 2600},
    {"id": "mp-1138", "formula": "W2C", "T_melt_C": 2785, "density": 17150, "hardness_HV": 2200},
    {"id": "mp-568", "formula": "Mo", "T_melt_C": 2623, "density": 10280, "hardness_HV": 1530},
    {"id": "mp-1634", "formula": "Mo2C", "T_melt_C": 2687, "density": 9180, "hardness_HV": 1950},
    {"id": "mp-1245", "formula": "TaC", "T_melt_C": 3880, "density": 14300, "hardness_HV": 2000},
    {"id": "mp-1269", "formula": "HfC", "T_melt_C": 3900, "density": 12200, "hardness_HV": 2600},
    {"id": "mp-742", "formula": "TiC", "T_melt_C": 3160, "density": 4930, "hardness_HV": 2850},
    {"id": "mp-1078", "formula": "NbC", "T_melt_C": 3600, "density": 7800, "hardness_HV": 2400},
    {"id": "mp-1289", "formula": "ZrC", "T_melt_C": 3400, "density": 6730, "hardness_HV": 2700},
    {"id": "mp-1001", "formula": "Ta4HfC5", "T_melt_C": 4215, "density": 14100, "hardness_HV": 2100},  # HIGHEST MELTING POINT KNOWN!
]


def get_offline_database() -> List[Dict]:
    """
    Returns curated high-temp materials for offline use.
    
    NOTE: Ta4HfC5 has the HIGHEST melting point of any known material: 4215°C!
    """
    return OFFLINE_HIGH_TEMP_MATERIALS


if __name__ == "__main__":
    print("=" * 60)
    print("Materials Project Database Interface")
    print("=" * 60)
    
    # Try online first
    api_key = os.getenv("MP_API_KEY")
    
    if api_key and MP_API_AVAILABLE:
        try:
            db = MaterialsProjectDB(api_key)
            print(f"\nSearching for tungsten compounds...")
            materials = db.get_all_tungsten_compounds(limit=10)
            for m in materials[:5]:
                print(f"  {m.formula}: {m.material_id} (stable={m.is_stable})")
        except Exception as e:
            print(f"API Error: {e}")
            print("\nUsing offline database...")
            for m in get_offline_database()[:5]:
                print(f"  {m['formula']}: T_melt={m['T_melt_C']}°C, HV={m['hardness_HV']}")
    else:
        print("\nNo API key. Using offline database (curated high-temp materials):")
        print("-" * 60)
        for m in get_offline_database():
            print(f"  {m['formula']:12s} | T_melt: {m['T_melt_C']:4d}°C | HV: {m['hardness_HV']}")
        
        print("\n" + "=" * 60)
        print("HIGHEST MELTING POINT KNOWN: Ta4HfC5 at 4215°C!")
        print("=" * 60)
