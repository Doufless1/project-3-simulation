// ============================================================================
// Chamber Configuration — Pure domain value objects for chamber switching.
//
// No Unity dependencies. Defines 5 chamber types with dimensions,
// material constraints, and travel limits for the X-Y table.
//
// OCP: Add new chamber types by extending the enum and specs dictionary.
// ============================================================================

using System.Collections.Generic;

namespace HVOFSim.Domain.ValueObjects
{
    /// <summary>Available processing chamber configurations.</summary>
    public enum ChamberType
    {
        StandardAluminium,
        LargeSteel,
        OpenAir,
        Vacuum,
        Custom
    }

    /// <summary>Available window materials for laser transmission.</summary>
    public enum WindowType
    {
        FusedSilica,
        ZnSe,
        Sapphire,
        None
    }

    /// <summary>
    /// Immutable specification for a chamber configuration.
    /// Contains dimensions, material info, and operational constraints.
    /// </summary>
    public readonly struct ChamberSpec
    {
        public readonly string Name;
        public readonly string MaterialName;
        public readonly double LengthMm;
        public readonly double WidthMm;
        public readonly double HeightMm;
        public readonly WindowType Window;
        public readonly int GasInlets;
        public readonly double TravelXMm;
        public readonly double TravelYMm;
        public readonly bool SupportsGas;
        public readonly bool IsVacuum;
        public readonly string Description;

        public ChamberSpec(
            string name, string materialName,
            double lengthMm, double widthMm, double heightMm,
            WindowType window, int gasInlets,
            double travelXMm, double travelYMm,
            bool supportsGas, bool isVacuum,
            string description)
        {
            Name = name;
            MaterialName = materialName;
            LengthMm = lengthMm;
            WidthMm = widthMm;
            HeightMm = heightMm;
            Window = window;
            GasInlets = gasInlets;
            TravelXMm = travelXMm;
            TravelYMm = travelYMm;
            SupportsGas = supportsGas;
            IsVacuum = isVacuum;
            Description = description;
        }
    }

    /// <summary>
    /// Static catalog of chamber specifications.
    /// Pure data — no side effects.
    /// </summary>
    public static class ChamberSpecs
    {
        private static readonly Dictionary<ChamberType, ChamberSpec> _specs = new()
        {
            [ChamberType.StandardAluminium] = new ChamberSpec(
                name: "Standard Aluminium Chamber",
                materialName: "6061-T6 Aluminium",
                lengthMm: 400, widthMm: 400, heightMm: 200,
                window: WindowType.FusedSilica,
                gasInlets: 2,
                travelXMm: 300, travelYMm: 300,
                supportsGas: true, isVacuum: false,
                description: "Default processing chamber. Fused silica window for NIR/visible laser transmission. 2× KF-25 gas inlets."
            ),
            [ChamberType.LargeSteel] = new ChamberSpec(
                name: "Large Steel Chamber",
                materialName: "304 Stainless Steel",
                lengthMm: 600, widthMm: 600, heightMm: 300,
                window: WindowType.FusedSilica,
                gasInlets: 4,
                travelXMm: 500, travelYMm: 500,
                supportsGas: true, isVacuum: false,
                description: "For larger samples. Stainless steel construction, 4 gas inlets for uniform purging."
            ),
            [ChamberType.OpenAir] = new ChamberSpec(
                name: "Open Air (No Chamber)",
                materialName: "—",
                lengthMm: 0, widthMm: 0, heightMm: 0,
                window: WindowType.None,
                gasInlets: 0,
                travelXMm: 500, travelYMm: 500,
                supportsGas: false, isVacuum: false,
                description: "No enclosure — outdoor or large-scale processing. No gas shielding possible. Oxidation will occur."
            ),
            [ChamberType.Vacuum] = new ChamberSpec(
                name: "Vacuum Chamber",
                materialName: "304 Stainless Steel",
                lengthMm: 400, widthMm: 400, heightMm: 250,
                window: WindowType.Sapphire,
                gasInlets: 0,
                travelXMm: 300, travelYMm: 300,
                supportsGas: false, isVacuum: true,
                description: "Sealed vacuum chamber pumped to 10⁻³ mbar. Sapphire window for broad spectral transmission. No gas flow — vacuum environment."
            ),
            [ChamberType.Custom] = new ChamberSpec(
                name: "Custom Chamber",
                materialName: "User Defined",
                lengthMm: 400, widthMm: 400, heightMm: 200,
                window: WindowType.FusedSilica,
                gasInlets: 2,
                travelXMm: 300, travelYMm: 300,
                supportsGas: true, isVacuum: false,
                description: "User-defined chamber. Specify material, dimensions, window type, and number of gas inlets."
            )
        };

        /// <summary>Get the specification for a chamber type.</summary>
        public static ChamberSpec GetSpec(ChamberType type) => _specs[type];

        /// <summary>Get all available chamber specs.</summary>
        public static IReadOnlyDictionary<ChamberType, ChamberSpec> All => _specs;

        /// <summary>Create a custom chamber spec with user-defined dimensions.</summary>
        public static ChamberSpec CreateCustom(
            double lengthMm, double widthMm, double heightMm,
            WindowType window = WindowType.FusedSilica,
            int gasInlets = 2,
            string materialName = "User Defined")
        {
            // Travel limits are 80% of internal dimensions (leave margin for fixtures)
            double travelX = lengthMm * 0.8;
            double travelY = widthMm * 0.8;

            return new ChamberSpec(
                name: "Custom Chamber",
                materialName: materialName,
                lengthMm: lengthMm, widthMm: widthMm, heightMm: heightMm,
                window: window,
                gasInlets: gasInlets,
                travelXMm: travelX, travelYMm: travelY,
                supportsGas: gasInlets > 0, isVacuum: false,
                description: $"Custom {lengthMm}×{widthMm}×{heightMm} mm chamber. {materialName}."
            );
        }
    }
}
