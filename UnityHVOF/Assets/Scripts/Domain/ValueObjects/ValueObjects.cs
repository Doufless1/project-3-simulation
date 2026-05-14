// ============================================================================
// Value Objects — Immutable, self-validating data containers.
//
// CIA Triad:
//   - Integrity: Every value is validated on construction.
//   - Confidentiality: Readonly structs prevent post-creation tampering.
//
// Port of: domain/value_objects.py + lab_control/domain/value_objects.py
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace HVOFSim.Domain.ValueObjects
{
    // ── 3D Position ──────────────────────────────────────────────────

    /// <summary>Immutable 3D coordinate in meters.</summary>
    public readonly struct Position3D
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Position3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>Euclidean distance to another point.</summary>
        public double DistanceTo(Position3D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4}) m";
    }

    // ── 2D Position ──────────────────────────────────────────────────

    /// <summary>Immutable X-Y coordinate in millimetres.</summary>
    public readonly struct Position2D
    {
        public readonly double XMm;
        public readonly double YMm;

        public Position2D(double xMm, double yMm)
        {
            if (double.IsNaN(xMm) || double.IsInfinity(xMm))
                throw new ArgumentException("x_mm must be a finite number.", nameof(xMm));
            if (double.IsNaN(yMm) || double.IsInfinity(yMm))
                throw new ArgumentException("y_mm must be a finite number.", nameof(yMm));

            XMm = xMm;
            YMm = yMm;
        }

        /// <summary>Euclidean distance to another 2D point [mm].</summary>
        public double DistanceTo(Position2D other)
        {
            double dx = XMm - other.XMm;
            double dy = YMm - other.YMm;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString() => $"({XMm:F2}, {YMm:F2}) mm";
    }

    // ── Temperature ──────────────────────────────────────────────────

    /// <summary>
    /// Validated temperature value in Celsius.
    /// Enforces physical lower bound (absolute zero = -273.15 °C).
    /// </summary>
    public readonly struct Temperature
    {
        public readonly double ValueCelsius;

        public Temperature(double valueCelsius)
        {
            if (valueCelsius < -273.15)
                throw new ArgumentException(
                    $"Temperature {valueCelsius}°C is below absolute zero.");

            ValueCelsius = valueCelsius;
        }

        /// <summary>Convert to Kelvin.</summary>
        public double Kelvin => ValueCelsius + 273.15;

        /// <summary>Check if this temperature exceeds a threshold.</summary>
        public bool Exceeds(double thresholdCelsius) => ValueCelsius > thresholdCelsius;

        public override string ToString() => $"{ValueCelsius:F1} °C";
    }

    // ── Speed ────────────────────────────────────────────────────────

    /// <summary>Validated scan speed in mm/s.</summary>
    public readonly struct Speed
    {
        public readonly double ValueMmPerS;

        public Speed(double valueMmPerS)
        {
            if (valueMmPerS < 0)
                throw new ArgumentException(
                    $"Speed cannot be negative: {valueMmPerS} mm/s");

            ValueMmPerS = valueMmPerS;
        }

        public bool IsZero => ValueMmPerS == 0.0;

        public override string ToString() => $"{ValueMmPerS:F1} mm/s";
    }

    // ── Flow Rate ────────────────────────────────────────────────────

    /// <summary>Validated gas flow rate in L/min.</summary>
    public readonly struct FlowRate
    {
        public readonly double ValueLPerMin;

        public FlowRate(double valueLPerMin)
        {
            if (valueLPerMin < 0)
                throw new ArgumentException(
                    $"Flow rate cannot be negative: {valueLPerMin} L/min");

            ValueLPerMin = valueLPerMin;
        }

        public override string ToString() => $"{ValueLPerMin:F1} L/min";
    }

    // ── Composition ──────────────────────────────────────────────────

    /// <summary>
    /// Normalized element-to-mass-fraction mapping.
    /// Integrity: Validates fractions are non-negative and auto-normalizes.
    /// </summary>
    public sealed class Composition
    {
        private readonly Dictionary<string, double> _elements;

        public IReadOnlyDictionary<string, double> Elements => _elements;

        public Composition(Dictionary<string, double> elements)
        {
            if (elements == null || elements.Count == 0)
                throw new ArgumentException("Composition must have at least one element.");

            foreach (var kvp in elements)
            {
                if (kvp.Value < 0)
                    throw new ArgumentException(
                        $"Negative fraction {kvp.Value} for element '{kvp.Key}'.");
            }

            double total = elements.Values.Sum();
            if (total <= 0)
                throw new ArgumentException("Composition must have at least one element.");

            if (total < 0.99 || total > 1.01)
            {
                _elements = elements.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value / total);
            }
            else
            {
                _elements = new Dictionary<string, double>(elements);
            }
        }

        /// <summary>Return elements with fraction > 10%, sorted descending.</summary>
        public List<KeyValuePair<string, double>> DominantElements =>
            _elements
                .Where(kvp => kvp.Value > 0.10)
                .OrderByDescending(kvp => kvp.Value)
                .ToList();
    }

    // ── G-Code Line ──────────────────────────────────────────────────

    /// <summary>Single validated G-code instruction.</summary>
    public readonly struct GCodeLine
    {
        public readonly string Code;
        public readonly int LineNumber;

        public GCodeLine(string code, int lineNumber)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("G-code line cannot be empty.");
            if (lineNumber < 0)
                throw new ArgumentException("Line number must be non-negative.");

            Code = code;
            LineNumber = lineNumber;
        }

        public override string ToString() => $"N{LineNumber} {Code}";
    }

    // ── Scan Recipe ──────────────────────────────────────────────────

    /// <summary>
    /// Immutable scan parameters recipe.
    /// Integrity: All values validated on construction.
    /// </summary>
    public readonly struct ScanRecipe
    {
        private static readonly string[] ValidPatterns = { "raster", "spiral", "linear" };

        public readonly string Pattern;
        public readonly double SpeedMmS;
        public readonly double PowerW;
        public readonly double SpotMm;
        public readonly double OverlapPct;
        public readonly double WidthMm;
        public readonly double HeightMm;

        public ScanRecipe(
            string pattern, double speedMmS, double powerW,
            double spotMm, double overlapPct, double widthMm, double heightMm)
        {
            if (Array.IndexOf(ValidPatterns, pattern) < 0)
                throw new ArgumentException(
                    $"Unknown pattern '{pattern}', must be one of: raster, spiral, linear");
            if (speedMmS <= 0) throw new ArgumentException("Speed must be positive.");
            if (powerW <= 0) throw new ArgumentException("Power must be positive.");
            if (spotMm <= 0) throw new ArgumentException("Spot diameter must be positive.");
            if (overlapPct < 0 || overlapPct > 99)
                throw new ArgumentException("Overlap must be 0–99%.");
            if (widthMm <= 0 || heightMm <= 0)
                throw new ArgumentException("Scan area dimensions must be positive.");

            Pattern = pattern;
            SpeedMmS = speedMmS;
            PowerW = powerW;
            SpotMm = spotMm;
            OverlapPct = overlapPct;
            WidthMm = widthMm;
            HeightMm = heightMm;
        }

        /// <summary>Distance between scan lines based on spot and overlap.</summary>
        public double LineSpacingMm => SpotMm * (1.0 - OverlapPct / 100.0);
    }

    // ── Equipment Spec ───────────────────────────────────────────────

    /// <summary>Specification for a single piece of lab equipment.</summary>
    public readonly struct EquipmentSpec
    {
        public readonly string Category;
        public readonly string Name;
        public readonly string Description;
        public readonly double UnitCostEur;
        public readonly int Quantity;

        public EquipmentSpec(
            string category, string name, string description,
            double unitCostEur, int quantity = 1)
        {
            if (unitCostEur < 0) throw new ArgumentException("Cost cannot be negative.");
            if (quantity < 1) throw new ArgumentException("Quantity must be at least 1.");

            Category = category;
            Name = name;
            Description = description;
            UnitCostEur = unitCostEur;
            Quantity = quantity;
        }

        /// <summary>Total cost for this line item.</summary>
        public double TotalCostEur => UnitCostEur * Quantity;
    }
}
