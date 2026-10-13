"""
Equipment Cost Catalog — In-memory equipment database.

CIA Triad:
  - Integrity: EquipmentSpec value objects prevent negative costs.
  - Availability: Catalog is immutable after construction.

Pattern: Repository
"""

from typing import List

from lab_control.domain.value_objects import EquipmentSpec
from lab_control.domain.ports import ICostCalculator
from lab_control.domain.exceptions import InvalidComponentError


# ── Equipment Database ────────────────────────────────────────────────

_DEFAULT_CATALOG: List[EquipmentSpec] = [
    # ── Laser System ──────────────────
    EquipmentSpec(
        category="laser",
        name="IPG YLR-1000 Fiber Laser",
        description="1 kW CW Ytterbium fiber laser, 1070 nm",
        unit_cost_eur=60000.0,
    ),
    EquipmentSpec(
        category="laser",
        name="Processing Head (Collimator + Focus)",
        description="Beam delivery head with f=100/200 mm optics",
        unit_cost_eur=4000.0,
    ),
    EquipmentSpec(
        category="laser",
        name="Armoured Process Fibre (10 m)",
        description="Single-mode armoured fibre, QBH connector",
        unit_cost_eur=2500.0,
    ),
    EquipmentSpec(
        category="laser",
        name="Protective Windows",
        description="Fused silica, AR-coated at 1070 nm",
        unit_cost_eur=100.0,
        quantity=5,
    ),

    # ── X-Y Table ─────────────────────
    EquipmentSpec(
        category="stage",
        name="PI M-531.DD Linear Stage",
        description="300 mm travel, 1 µm resolution, crossed-roller",
        unit_cost_eur=5500.0,
        quantity=2,
    ),
    EquipmentSpec(
        category="stage",
        name="Stepper Motors + Encoders",
        description="NEMA 23 with 10,000 count encoders",
        unit_cost_eur=1100.0,
        quantity=2,
    ),
    EquipmentSpec(
        category="stage",
        name="PI C-884 Motion Controller",
        description="2-axis motion controller, Python API",
        unit_cost_eur=4500.0,
    ),

    # ── Optical Table ─────────────────
    EquipmentSpec(
        category="structure",
        name="Optical Breadboard 1200×900 mm",
        description="Stainless steel, M6 holes, honeycomb core",
        unit_cost_eur=3500.0,
    ),
    EquipmentSpec(
        category="structure",
        name="Vibration Isolation Legs",
        description="Passive pneumatic isolators",
        unit_cost_eur=800.0,
        quantity=4,
    ),
    EquipmentSpec(
        category="structure",
        name="Mounting Posts + Holders + Brackets",
        description="Stainless steel post assembly for laser head",
        unit_cost_eur=1500.0,
    ),

    # ── Processing Chamber ────────────
    EquipmentSpec(
        category="chamber",
        name="Aluminium Processing Chamber",
        description="6061-T6, 400×400×200 mm, quartz window",
        unit_cost_eur=4000.0,
    ),

    # ── Gas System ────────────────────
    EquipmentSpec(
        category="gas",
        name="Mass Flow Controller (MFC)",
        description="Bronkhorst EL-FLOW, 0-20 L/min Argon",
        unit_cost_eur=1800.0,
    ),
    EquipmentSpec(
        category="gas",
        name="Gas Regulator + Fittings",
        description="Two-stage regulator, tubing, KF flanges",
        unit_cost_eur=600.0,
    ),
    EquipmentSpec(
        category="gas",
        name="Argon Cylinder 50 L",
        description="High-purity 99.999%, 200 bar",
        unit_cost_eur=350.0,
    ),
    EquipmentSpec(
        category="gas",
        name="Inline O₂ Sensor",
        description="Electrochemical O₂ monitor, 0-25% range",
        unit_cost_eur=800.0,
    ),

    # ── Safety ────────────────────────
    EquipmentSpec(
        category="safety",
        name="Safety Relay Module (Pilz PNOZ)",
        description="SIL 3 rated, dual-channel E-stop",
        unit_cost_eur=450.0,
    ),
    EquipmentSpec(
        category="safety",
        name="Door Interlock Switch",
        description="Schmersal magnetic safety switch",
        unit_cost_eur=180.0,
        quantity=2,
    ),
    EquipmentSpec(
        category="safety",
        name="Emergency Stop Button",
        description="Mushroom-head, latching, dual-channel",
        unit_cost_eur=85.0,
        quantity=2,
    ),
    EquipmentSpec(
        category="safety",
        name="Warning Beacon + Signs",
        description="Rotating red/amber beacon, Class 4 Laser signs",
        unit_cost_eur=300.0,
    ),
    EquipmentSpec(
        category="safety",
        name="Laser Safety Eyewear (OD 7+)",
        description="1070 nm protection, OD 7+, 5 pairs",
        unit_cost_eur=350.0,
        quantity=5,
    ),

    # ── Extraction ────────────────────
    EquipmentSpec(
        category="extraction",
        name="Fume Extraction System",
        description="HEPA H14 + activated carbon, centrifugal blower",
        unit_cost_eur=4000.0,
    ),

    # ── Electrical ────────────────────
    EquipmentSpec(
        category="electrical",
        name="Power Distribution + RCD",
        description="RCD-protected outlets, wiring, grounding",
        unit_cost_eur=2000.0,
    ),
    EquipmentSpec(
        category="electrical",
        name="Control PC + Monitors",
        description="Desktop PC for lab control software",
        unit_cost_eur=1800.0,
    ),

    # ── Temperature Monitoring ────────
    EquipmentSpec(
        category="monitoring",
        name="PID Temperature Controller",
        description="Eurotherm 3216, for heater plate control",
        unit_cost_eur=650.0,
    ),
    EquipmentSpec(
        category="monitoring",
        name="K-Type Thermocouples",
        description="Ø 1 mm, 4× for sample monitoring",
        unit_cost_eur=45.0,
        quantity=4,
    ),
    EquipmentSpec(
        category="monitoring",
        name="IR Pyrometer",
        description="Non-contact, for melt pool temperature",
        unit_cost_eur=2500.0,
    ),

    # ── Installation ──────────────────
    EquipmentSpec(
        category="installation",
        name="Lab Preparation Works",
        description="Electrical, ventilation, structural modifications",
        unit_cost_eur=7500.0,
    ),
    EquipmentSpec(
        category="installation",
        name="Equipment Installation + Alignment",
        description="Professional installation labour",
        unit_cost_eur=4000.0,
    ),
    EquipmentSpec(
        category="installation",
        name="Safety Certification + Inspection",
        description="Laser safety audit, electrical inspection",
        unit_cost_eur=3000.0,
    ),
]


class EquipmentCostCatalog(ICostCalculator):
    """
    In-memory equipment cost calculator.

    Follows Repository pattern with an immutable default catalog.
    """

    def __init__(self, catalog: List[EquipmentSpec] = None):
        self._catalog = list(catalog or _DEFAULT_CATALOG)

    def get_catalog(self) -> List[EquipmentSpec]:
        """Return a copy of the full equipment catalog."""
        return list(self._catalog)

    def calculate_total(
        self, selected_items: List[EquipmentSpec]
    ) -> float:
        """Sum total cost of selected equipment."""
        return sum(item.total_cost_eur for item in selected_items)

    def get_by_category(self, category: str) -> List[EquipmentSpec]:
        """Filter equipment by category string."""
        matches = [
            item for item in self._catalog
            if item.category == category
        ]
        if not matches:
            raise InvalidComponentError(
                f"No equipment found in category '{category}'"
            )
        return matches

    def get_categories(self) -> List[str]:
        """Return sorted list of unique categories."""
        return sorted(set(item.category for item in self._catalog))
