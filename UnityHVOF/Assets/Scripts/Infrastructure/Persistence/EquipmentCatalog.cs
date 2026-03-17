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
            // Laser system
            new EquipmentSpec("laser", "IPG YLR-1000", "1 kW CW fiber laser source", 45000),
            new EquipmentSpec("laser", "Laser Head", "Processing head with collimator", 8000),
            new EquipmentSpec("laser", "Chiller", "Laser cooling unit", 5000),

            // Motion system
            new EquipmentSpec("stage", "X-Y Linear Stage", "300mm travel, 1µm resolution", 12000, 2),
            new EquipmentSpec("stage", "Motion Controller", "2-axis servo controller", 4500),
            new EquipmentSpec("stage", "Workpiece Fixture", "Vacuum chuck", 1500),

            // Chamber and gas
            new EquipmentSpec("chamber", "Process Chamber", "Sealed inert atmosphere enclosure", 15000),
            new EquipmentSpec("chamber", "Argon Supply", "Gas cylinder + regulator", 800, 2),
            new EquipmentSpec("chamber", "Mass Flow Controller", "0-20 L/min Ar MFC", 3500),
            new EquipmentSpec("chamber", "O2 Analyzer", "Trace oxygen sensor", 4000),

            // Safety
            new EquipmentSpec("safety", "Safety Enclosure", "Class 4 laser safety enclosure", 8000),
            new EquipmentSpec("safety", "Interlock System", "Door + chamber interlocks", 2500),
            new EquipmentSpec("safety", "E-Stop System", "Emergency stop with relay", 500),
            new EquipmentSpec("safety", "Safety Glasses", "OD7+ laser safety eyewear", 350, 4),
            new EquipmentSpec("safety", "Warning System", "Laser warning lights + signs", 600),

            // Diagnostics
            new EquipmentSpec("diagnostics", "Pyrometer", "Non-contact IR thermometer", 6000),
            new EquipmentSpec("diagnostics", "Power Meter", "Laser power measurement", 3000),
            new EquipmentSpec("diagnostics", "Camera System", "Process monitoring camera", 4500),

            // Software and control
            new EquipmentSpec("control", "Control PC", "Industrial control computer", 2000),
            new EquipmentSpec("control", "DAQ System", "Data acquisition hardware", 5000),
            new EquipmentSpec("control", "Control Software", "Custom lab control software", 0),
        };
    }
}
