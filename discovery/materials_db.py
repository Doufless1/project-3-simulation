"""
Universal Element Properties.
Physical constants for base elements used in alloy calculations.
"""

from dataclasses import dataclass
from typing import Dict

@dataclass
class Element:
    symbol: str
    name: str
    melt_temp: float      # °C
    vapor_temp: float     # °C
    density: float        # kg/m^3
    thermal_cond: float   # W/m·K
    specific_heat: float  # J/kg·K
    hardness: float       # Vickers (HV) - Approximate
    cost_per_kg: float    # USD (Approximate)

# Validated physical properties (Source: ASM Handbook / WebElements)
ELEMENTS = {
    'Fe': Element('Fe', 'Iron', 1538, 2862, 7874, 80.0, 449, 608, 0.5),
    'Ni': Element('Ni', 'Nickel', 1455, 2913, 8908, 91.0, 445, 638, 20.0),
    'Cr': Element('Cr', 'Chromium', 1907, 2671, 7190, 93.0, 448, 1060, 12.0),
    'Co': Element('Co', 'Cobalt', 1495, 2927, 8900, 100.0, 421, 1043, 30.0),
    'W':  Element('W', 'Tungsten', 3422, 5930, 19250, 173.0, 132, 3430, 45.0),
    'Ti': Element('Ti', 'Titanium', 1668, 3287, 4506, 21.9, 520, 970, 15.0),
    'C':  Element('C', 'Carbon', 3550, 4827, 2267, 140.0, 710, 10000, 2.0),
    'Mo': Element('Mo', 'Molybdenum', 2623, 4639, 10280, 138.0, 251, 1530, 35.0),
    'Ta': Element('Ta', 'Tantalum', 3017, 5458, 16690, 57.5, 140, 873, 150.0),
    'Hf': Element('Hf', 'Hafnium', 2233, 4603, 13310, 23.0, 144, 1760, 300.0),
    'Nb': Element('Nb', 'Niobium', 2477, 4744, 8570, 53.7, 265, 1320, 45.0),
    'V':  Element('V', 'Vanadium', 1910, 3407, 6110, 30.7, 489, 628, 25.0),
    'Zr': Element('Zr', 'Zirconium', 1855, 4409, 6520, 22.7, 278, 903, 30.0)
}
