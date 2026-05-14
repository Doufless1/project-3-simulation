// ============================================================================
// Equipment Catalog — Lab equipment cost data.
//
// Pattern: Repository — implements ICostCalculator.
// Hardcoded catalog matching the Python implementation.
//
// Port of: lab_control/infrastructure/equipment_catalog.py
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using HVOFSim.Domain.Ports;
using HVOFSim.Domain.ValueObjects;

namespace HVOFSim.Infrastructure.Persistence
{
    /// <summary>Lab equipment catalog with cost calculation.</summary>
    public sealed class EquipmentCatalog : ICostCalculator
    {
        private readonly List<EquipmentSpec> _catalog;

        public EquipmentCatalog()
        {
            _catalog = BuildDefaultCatalog();
        }

        public List<EquipmentSpec> GetCatalog() => new(_catalog);

        public double CalculateTotal(List<EquipmentSpec> selectedItems) =>
            selectedItems.Sum(item => item.TotalCostEur);

        public List<EquipmentSpec> GetByCategory(string category) =>
            _catalog.Where(e => e.Category == category).ToList();

        private static List<EquipmentSpec> BuildDefaultCatalog() => new()
        {
            // ── Laser System ─────────────────────────────────────────
            new EquipmentSpec("laser", "IPG YLR-300-AC", "300 W CW fiber laser source", 20500),
            new EquipmentSpec("laser", "Fiber Optic Cable", "Laser delivery fiber optic cable", 12414),
            new EquipmentSpec("laser", "Processing Head", "Laser cutting/processing head with collimator", 2619),
            new EquipmentSpec("laser", "Focusing Lenses", "F-theta focus lens for processing head", 162, 3),
            new EquipmentSpec("laser", "Fume Extractor", "Laser engraver fume extractor", 1716),

            // ── XY Table Components ──────────────────────────────────
            new EquipmentSpec("xytable", "TB6600 Stepper Motor Driver", "TopDirect stepper motor driver", 14, 2),
            new EquipmentSpec("xytable", "Arduino UNO Rev3", "Microcontroller for CNC control", 29.30),
            new EquipmentSpec("xytable", "CNC Shield v3", "CNC shield for Arduino", 2.95),
            new EquipmentSpec("xytable", "PC to Arduino Cable", "USB connection cable", 5.14),
            new EquipmentSpec("xytable", "Power Supply 12V 5A", "BLUBOTY 12V 5A power supply", 13.42),
            new EquipmentSpec("xytable", "Bus Cable 2x2x0.8mm 25m", "Lumonic shielded bus cable", 19.59),
            new EquipmentSpec("xytable", "Nema 17 Bipolar Stepper", "Stepper motor for XY motion", 16.82),

            // ── Safety Enclosure ─────────────────────────────────────
            new EquipmentSpec("enclosure", "3030 Aluminium Extrusion 400mm", "40×40×40 cm frame extrusion", 11.08, 12),
            new EquipmentSpec("enclosure", "M5 Bolts + T-slot Nuts Kit", "Fastener kit for extrusion frame", 9.79),
            new EquipmentSpec("enclosure", "Laser Warning LED Indicator", "LED warning light", 2.51),
            new EquipmentSpec("enclosure", "Door Handle", "Enclosure door handle", 1.80),
            new EquipmentSpec("enclosure", "Hinges", "Door hinges", 6.40),
            new EquipmentSpec("enclosure", "Door Microswitch Interlock", "Safety interlock microswitch", 16.24),
            new EquipmentSpec("enclosure", "Galvanized Steel Sheet 2mm", "40×40 cm panels for enclosure walls", 6, 6),
            new EquipmentSpec("enclosure", "Door Interlock Controller", "Safety interlock controller system", 1210),
            new EquipmentSpec("enclosure", "Illuminated Sign (Laser On)", "Laser active warning sign", 26),
            new EquipmentSpec("enclosure", "Magnetic Door Switches", "Magnetic reed door sensors", 32),
            new EquipmentSpec("enclosure", "Laser Safety Curtains", "Class 4 laser safety curtains", 1165, 3),

            // ── Protective Wear ──────────────────────────────────────
            new EquipmentSpec("protective", "CO2 Fire Extinguisher", "Carbon dioxide fire extinguisher", 90, 2),
            new EquipmentSpec("protective", "Protective Eyewear", "Laser safety goggles", 30),
            new EquipmentSpec("protective", "Gloves", "Protective work gloves", 69),
            new EquipmentSpec("protective", "Protective Earwear", "Ear protection / earmuffs", 132),

            // ── Labor Costs ──────────────────────────────────────────
            new EquipmentSpec("labor", "Lead Integrator", "80 hr @ €115/hr", 9200),
            new EquipmentSpec("labor", "Electrician", "24 hr @ €85/hr", 2040),
            new EquipmentSpec("labor", "LSO Consultant", "One time fee", 4000),
            new EquipmentSpec("labor", "Installation Technicians", "60 hr @ €45/hr", 2700),
            new EquipmentSpec("labor", "Contingency", "20% of total labor costs", 3588),
        };
    }
}
