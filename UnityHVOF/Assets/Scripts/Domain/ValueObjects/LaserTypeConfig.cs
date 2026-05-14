// ============================================================================
// Laser Type Configuration — Pure domain value objects for laser type switching.
//
// No Unity dependencies. Defines the 4 laser types (Fiber, CO₂, Nd:YAG, Diode)
// with their physical specifications.
//
// OCP: Add new laser types by extending the enum and specs dictionary.
// ============================================================================

using System.Collections.Generic;

namespace HVOFSim.Domain.ValueObjects
{
    /// <summary>Available laser source types.</summary>
    public enum LaserType
    {
        YtterbiumFiber,
        CO2,
        NdYAG,
        Diode
    }

    /// <summary>Beam intensity profile shape.</summary>
    public enum BeamProfile
    {
        Gaussian,
        TopHat
    }

    /// <summary>
    /// Immutable specification for a laser type.
    /// Contains physical parameters and display metadata.
    /// </summary>
    public readonly struct LaserTypeSpec
    {
        public readonly string Name;
        public readonly string ShortName;
        public readonly double WavelengthNm;
        public readonly double MinPowerW;
        public readonly double MaxPowerW;
        public readonly double BeamQualityM2;
        public readonly double EfficiencyPct;
        public readonly string BestFor;
        public readonly string Pros;
        public readonly string Cons;
        public readonly BeamProfile DefaultProfile;

        public LaserTypeSpec(
            string name, string shortName,
            double wavelengthNm, double minPowerW, double maxPowerW,
            double beamQualityM2, double efficiencyPct,
            string bestFor, string pros, string cons,
            BeamProfile defaultProfile)
        {
            Name = name;
            ShortName = shortName;
            WavelengthNm = wavelengthNm;
            MinPowerW = minPowerW;
            MaxPowerW = maxPowerW;
            BeamQualityM2 = beamQualityM2;
            EfficiencyPct = efficiencyPct;
            BestFor = bestFor;
            Pros = pros;
            Cons = cons;
            DefaultProfile = defaultProfile;
        }

        /// <summary>Wavelength in meters for physics calculations.</summary>
        public double WavelengthM => WavelengthNm * 1e-9;
    }

    /// <summary>
    /// Static catalog of laser type specifications.
    /// Pure data — no side effects.
    /// </summary>
    public static class LaserTypeSpecs
    {
        private static readonly Dictionary<LaserType, LaserTypeSpec> _specs = new()
        {
            [LaserType.YtterbiumFiber] = new LaserTypeSpec(
                name: "Ytterbium Fiber Laser",
                shortName: "Yb Fiber",
                wavelengthNm: 1070,
                minPowerW: 50,
                maxPowerW: 100000,
                beamQualityM2: 1.1,
                efficiencyPct: 40,
                bestFor: "Metals — best beam quality, compact, low maintenance",
                pros: "Excellent beam quality (M2~1.1), compact size, air-cooled options, 100k+ hr diode life, fiber delivery",
                cons: "Higher cost per watt at lower powers, back-reflection sensitivity with Cu/Al",
                defaultProfile: BeamProfile.Gaussian
            ),
            [LaserType.CO2] = new LaserTypeSpec(
                name: "CO₂ Laser",
                shortName: "CO2",
                wavelengthNm: 10600,
                minPowerW: 100,
                maxPowerW: 45000,
                beamQualityM2: 2.5,
                efficiencyPct: 12,
                bestFor: "Ceramics — high absorption at 10.6 µm, large footprint",
                pros: "Excellent absorption by ceramics/organics, proven technology, high CW power available",
                cons: "Large footprint, mirror beam delivery (no fiber), water cooling required, low wall-plug efficiency",
                defaultProfile: BeamProfile.Gaussian
            ),
            [LaserType.NdYAG] = new LaserTypeSpec(
                name: "Nd:YAG Laser",
                shortName: "Nd:YAG",
                wavelengthNm: 1064,
                minPowerW: 50,
                maxPowerW: 6000,
                beamQualityM2: 3.5,
                efficiencyPct: 15,
                bestFor: "Pulsed work — good fiber coupling, lamp replacement needed",
                pros: "Good fiber coupling, pulsed modes available, well-understood, frequency doubling possible",
                cons: "Flash lamp lifetime (500-1000 hr), lower beam quality, water cooling, lower efficiency than fiber",
                defaultProfile: BeamProfile.Gaussian
            ),
            [LaserType.Diode] = new LaserTypeSpec(
                name: "Diode Laser",
                shortName: "Diode",
                wavelengthNm: 940,
                minPowerW: 100,
                maxPowerW: 20000,
                beamQualityM2: 12,
                efficiencyPct: 55,
                bestFor: "Wide area treatment — uniform top-hat profile, limited focusing",
                pros: "Highest wall-plug efficiency (50-60%), natural top-hat profile, compact, long lifetime, low cost per watt",
                cons: "Poor beam quality (M2>10), limited focusing ability, large spot sizes only",
                defaultProfile: BeamProfile.TopHat
            )
        };

        /// <summary>Get the specification for a laser type.</summary>
        public static LaserTypeSpec GetSpec(LaserType type) => _specs[type];

        /// <summary>Get all available laser type specs.</summary>
        public static IReadOnlyDictionary<LaserType, LaserTypeSpec> All => _specs;
    }
}
