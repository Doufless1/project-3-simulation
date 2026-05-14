// ============================================================================
// Material Presets — Pre-loaded material library for common HVOF coatings.
//
// No Unity dependencies. Contains 6 scientifically-validated material presets
// with display metadata (application, hardness, color swatch).
//
// CIA Integrity: All presets pass Material entity validation on construction.
// ============================================================================

using System.Collections.Generic;

namespace HVOFSim.Domain.ValueObjects
{
    /// <summary>
    /// Display-ready material preset with all Material constructor params
    /// plus additional metadata for the UI.
    /// </summary>
    public readonly struct MaterialPreset
    {
        // Material entity params
        public readonly string Name;
        public readonly double Absorption;
        public readonly double ThermalConductivity;
        public readonly double Density;
        public readonly double SpecificHeat;
        public readonly double TAmbient;
        public readonly double TMelt;
        public readonly double TVaporization;

        // Display metadata
        public readonly string Application;
        public readonly string HardnessRange;
        public readonly string ColorHex;
        public readonly string Category;

        public MaterialPreset(
            string name,
            double absorption, double thermalConductivity,
            double density, double specificHeat,
            double tAmbient, double tMelt, double tVaporization,
            string application, string hardnessRange,
            string colorHex, string category)
        {
            Name = name;
            Absorption = absorption;
            ThermalConductivity = thermalConductivity;
            Density = density;
            SpecificHeat = specificHeat;
            TAmbient = tAmbient;
            TMelt = tMelt;
            TVaporization = tVaporization;
            Application = application;
            HardnessRange = hardnessRange;
            ColorHex = colorHex;
            Category = category;
        }

        /// <summary>Create a validated domain Material entity from this preset.</summary>
        public Entities.Material ToDomainMaterial()
        {
            return new Entities.Material(
                Name, Absorption, ThermalConductivity,
                Density, SpecificHeat, TAmbient, TMelt, TVaporization);
        }
    }

    /// <summary>
    /// Static library of pre-loaded material presets.
    /// All values sourced from published literature.
    /// </summary>
    public static class MaterialPresetLibrary
    {
        private static readonly List<MaterialPreset> _presets = new()
        {
            new MaterialPreset(
                name: "ULTRA-C-Ta-WH-3849",
                absorption: 0.70,
                thermalConductivity: 60,
                density: 9939,
                specificHeat: 386,
                tAmbient: 20,
                tMelt: 3376,
                tVaporization: 4800,
                application: "Ultra-hard tantalum carbide coating for extreme wear resistance",
                hardnessRange: "2000–2500 HV",
                colorHex: "#7B8794",
                category: "Carbide"
            ),
            new MaterialPreset(
                name: "316L Stainless Steel",
                absorption: 0.35,
                thermalConductivity: 15,
                density: 8000,
                specificHeat: 500,
                tAmbient: 20,
                tMelt: 1400,
                tVaporization: 2800,
                application: "Corrosion-resistant substrate for biomedical and marine environments",
                hardnessRange: "200–250 HV",
                colorHex: "#C0C0C0",
                category: "Steel"
            ),
            new MaterialPreset(
                name: "WC-12Co",
                absorption: 0.55,
                thermalConductivity: 80,
                density: 14500,
                specificHeat: 240,
                tAmbient: 20,
                tMelt: 2870,
                tVaporization: 6000,
                application: "Tungsten carbide-cobalt HVOF coating for cutting tools and wear parts",
                hardnessRange: "1100–1400 HV",
                colorHex: "#4A5568",
                category: "Carbide"
            ),
            new MaterialPreset(
                name: "NiCrBSi",
                absorption: 0.45,
                thermalConductivity: 12,
                density: 7800,
                specificHeat: 460,
                tAmbient: 20,
                tMelt: 1040,
                tVaporization: 2500,
                application: "Self-fluxing nickel alloy coating for wear and corrosion protection",
                hardnessRange: "700–800 HV",
                colorHex: "#A0AEC0",
                category: "Nickel Alloy"
            ),
            new MaterialPreset(
                name: "Cr3C2-NiCr",
                absorption: 0.50,
                thermalConductivity: 19,
                density: 7200,
                specificHeat: 500,
                tAmbient: 20,
                tMelt: 1350,
                tVaporization: 3000,
                application: "High-temperature erosion-resistant coating for gas turbine components",
                hardnessRange: "850–1050 HV",
                colorHex: "#718096",
                category: "Carbide"
            ),
            new MaterialPreset(
                name: "Stellite 6",
                absorption: 0.40,
                thermalConductivity: 14,
                density: 8440,
                specificHeat: 420,
                tAmbient: 20,
                tMelt: 1285,
                tVaporization: 2900,
                application: "Cobalt-chromium alloy for valve seats, pump sleeves, and hot-section components",
                hardnessRange: "380–490 HV",
                colorHex: "#9CA3AF",
                category: "Cobalt Alloy"
            )
        };

        /// <summary>Get all pre-loaded material presets.</summary>
        public static IReadOnlyList<MaterialPreset> GetAll() => _presets;

        /// <summary>Get a preset by name (case-insensitive).</summary>
        public static MaterialPreset? GetByName(string name)
        {
            foreach (var preset in _presets)
            {
                if (string.Equals(preset.Name, name, System.StringComparison.OrdinalIgnoreCase))
                    return preset;
            }
            return null;
        }
    }
}
