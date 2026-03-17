// ============================================================================
// Gas Configuration — Pure domain value objects for gas system switching.
//
// No Unity dependencies. Defines 5 gas modes (None, Argon, N₂, He, Ar+H₂)
// with their physical specifications and safety metadata.
//
// OCP: Add new gas types by extending the enum and specs dictionary.
// ============================================================================

using System.Collections.Generic;

namespace HVOFSim.Domain.ValueObjects
{
    /// <summary>Available shielding gas configurations.</summary>
    public enum GasType
    {
        None,
        Argon,
        Nitrogen,
        Helium,
        ArgonHydrogenMix
    }

    /// <summary>
    /// Immutable specification for a gas type.
    /// Contains flow limits, safety metadata, and display information.
    /// </summary>
    public readonly struct GasTypeSpec
    {
        public readonly string DisplayName;
        public readonly string Formula;
        public readonly string Description;
        public readonly double MaxFlowLMin;
        public readonly double RecommendedPurgeFlowLMin;
        public readonly string WarningNote;
        public readonly string CylinderColorHex;
        public readonly bool RequiresPurgeBeforeFiring;
        public readonly bool IsInert;
        public readonly string[] ReactiveWithElements;

        public GasTypeSpec(
            string displayName, string formula, string description,
            double maxFlowLMin, double recommendedPurgeFlowLMin,
            string warningNote, string cylinderColorHex,
            bool requiresPurgeBeforeFiring, bool isInert,
            string[] reactiveWithElements = null)
        {
            DisplayName = displayName;
            Formula = formula;
            Description = description;
            MaxFlowLMin = maxFlowLMin;
            RecommendedPurgeFlowLMin = recommendedPurgeFlowLMin;
            WarningNote = warningNote;
            CylinderColorHex = cylinderColorHex;
            RequiresPurgeBeforeFiring = requiresPurgeBeforeFiring;
            IsInert = isInert;
            ReactiveWithElements = reactiveWithElements ?? System.Array.Empty<string>();
        }
    }

    /// <summary>
    /// Static catalog of gas type specifications.
    /// Pure data — no side effects.
    /// </summary>
    public static class GasTypeSpecs
    {
        private static readonly Dictionary<GasType, GasTypeSpec> _specs = new()
        {
            [GasType.None] = new GasTypeSpec(
                displayName: "No Gas (Open Air)",
                formula: "—",
                description: "Processing without shielding gas",
                maxFlowLMin: 0,
                recommendedPurgeFlowLMin: 0,
                warningNote: "⚠ Oxidation will occur during processing",
                cylinderColorHex: "",
                requiresPurgeBeforeFiring: false,
                isInert: false
            ),
            [GasType.Argon] = new GasTypeSpec(
                displayName: "Argon",
                formula: "Ar",
                description: "Standard inert shielding, 99.999% purity",
                maxFlowLMin: 20,
                recommendedPurgeFlowLMin: 15,
                warningNote: "",
                cylinderColorHex: "#1B8C4A",
                requiresPurgeBeforeFiring: true,
                isInert: true
            ),
            [GasType.Nitrogen] = new GasTypeSpec(
                displayName: "Nitrogen",
                formula: "N₂",
                description: "Cheaper alternative, reactive with some metals",
                maxFlowLMin: 30,
                recommendedPurgeFlowLMin: 20,
                warningNote: "⚠ N₂ may form nitrides with Ti, Al, and Cr at elevated temperatures",
                cylinderColorHex: "#1A1A1A",
                requiresPurgeBeforeFiring: true,
                isInert: false,
                reactiveWithElements: new[] { "Ti", "Al", "Cr" }
            ),
            [GasType.Helium] = new GasTypeSpec(
                displayName: "Helium",
                formula: "He",
                description: "Better heat transfer, expensive",
                maxFlowLMin: 25,
                recommendedPurgeFlowLMin: 18,
                warningNote: "Higher thermal conductivity — better cooling but 10× cost of Argon",
                cylinderColorHex: "#8B4513",
                requiresPurgeBeforeFiring: true,
                isInert: true
            ),
            [GasType.ArgonHydrogenMix] = new GasTypeSpec(
                displayName: "Argon + 5% H₂ Mix",
                formula: "Ar/H₂",
                description: "Reducing atmosphere for oxide removal",
                maxFlowLMin: 20,
                recommendedPurgeFlowLMin: 15,
                warningNote: "Reducing gas — helps dissolve oxide inclusions. Handle H₂ with care.",
                cylinderColorHex: "#2E8B57",
                requiresPurgeBeforeFiring: true,
                isInert: false
            )
        };

        /// <summary>Get the specification for a gas type.</summary>
        public static GasTypeSpec GetSpec(GasType type) => _specs[type];

        /// <summary>Get all available gas type specs.</summary>
        public static IReadOnlyDictionary<GasType, GasTypeSpec> All => _specs;
    }
}
