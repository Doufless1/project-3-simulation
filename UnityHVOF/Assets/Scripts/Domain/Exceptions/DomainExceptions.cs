// ============================================================================
// Domain Exceptions — Custom error types for the simulation engine.
//
// Each exception has a single responsibility (SRP):
//   - InvalidMaterialException: Material property validation failures
//   - SolverInstabilityException: Numerical solver exceeded stability limits
//   - GridTooLargeException: Requested grid exceeds safe memory bounds
//   - InvalidTrajectoryException: Motion path validation failures
//   - InvalidLaserConfigException: Laser parameter validation failures
//
// Lab Control exceptions:
//   - TableLimitException, TableBusyException, TableNotHomedException
//   - LaserPowerException, LaserInterlockException
//   - GasFlowException, SafetyViolationException
// ============================================================================

using System;

namespace HVOFSim.Domain.Exceptions
{
    /// <summary>Base exception for all domain errors.</summary>
    public class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }
        public DomainException(string message, Exception inner) : base(message, inner) { }
    }

    // ── Simulation Exceptions ────────────────────────────────────────

    /// <summary>Raised when material properties fail validation.</summary>
    public sealed class InvalidMaterialException : DomainException
    {
        public InvalidMaterialException(string message) : base(message) { }
    }

    /// <summary>Raised when the numerical solver exceeds stability criteria.</summary>
    public sealed class SolverInstabilityException : DomainException
    {
        public SolverInstabilityException(string message) : base(message) { }
    }

    /// <summary>Raised when the requested simulation grid exceeds safe limits (Availability — DoS prevention).</summary>
    public sealed class GridTooLargeException : DomainException
    {
        public int RequestedCells { get; }
        public int MaxCells { get; }

        public GridTooLargeException(int requestedCells, int maxCells)
            : base($"Grid too large: {requestedCells:N0} cells requested, max allowed is {maxCells:N0}.")
        {
            RequestedCells = requestedCells;
            MaxCells = maxCells;
        }
    }

    /// <summary>Raised when trajectory arrays are inconsistent.</summary>
    public sealed class InvalidTrajectoryException : DomainException
    {
        public InvalidTrajectoryException(string message) : base(message) { }
    }

    /// <summary>Raised when laser parameters fail validation.</summary>
    public sealed class InvalidLaserConfigException : DomainException
    {
        public InvalidLaserConfigException(string message) : base(message) { }
    }

    // ── Lab Control Exceptions ───────────────────────────────────────

    /// <summary>Raised when table target is outside travel limits.</summary>
    public sealed class TableLimitException : DomainException
    {
        public TableLimitException(string message) : base(message) { }
    }

    /// <summary>Raised when table is busy and cannot accept commands.</summary>
    public sealed class TableBusyException : DomainException
    {
        public TableBusyException(string message) : base(message) { }
    }

    /// <summary>Raised when table has not been homed.</summary>
    public sealed class TableNotHomedException : DomainException
    {
        public TableNotHomedException(string message) : base(message) { }
    }

    /// <summary>Raised when laser power is out of range.</summary>
    public sealed class LaserPowerException : DomainException
    {
        public LaserPowerException(string message) : base(message) { }
    }

    /// <summary>Raised when laser interlock conditions are not met (STRIDE: Elevation of Privilege defense).</summary>
    public sealed class LaserInterlockException : DomainException
    {
        public LaserInterlockException(string message) : base(message) { }
    }

    /// <summary>Raised when gas flow rate is out of range.</summary>
    public sealed class GasFlowException : DomainException
    {
        public GasFlowException(string message) : base(message) { }
    }

    /// <summary>Raised when a safety violation is detected.</summary>
    public sealed class SafetyViolationException : DomainException
    {
        public SafetyViolationException(string message) : base(message) { }
    }
}
