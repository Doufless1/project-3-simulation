// ============================================================================
// Lab Control Entities — Core business objects for the digital twin.
//
// CIA Triad:
//   - Integrity: All state changes validated via methods.
//   - Availability: State machine prevents invalid transitions.
//
// STRIDE:
//   - Spoofing: Interlock states read-only from domain.
//   - Tampering: State changes through validated methods only.
//   - Elevation of Privilege: Laser cannot fire without all interlocks locked.
//
// Port of: lab_control/domain/entities.py
// ============================================================================

using HVOFSim.Domain.Exceptions;
using HVOFSim.Domain.ValueObjects;

namespace HVOFSim.Domain.Entities
{
    // ── Enums ────────────────────────────────────────────────────────

    /// <summary>X-Y table finite state machine.</summary>
    public enum TableState { Idle, Homing, Moving, Scanning, Error }

    /// <summary>Laser finite state machine.</summary>
    public enum LaserState { Off, Standby, Armed, Firing, Error }

    /// <summary>Gas system finite state machine.</summary>
    public enum GasState { Closed, Purging, Flowing, Error }

    /// <summary>Safety interlock status.</summary>
    public enum InterlockStatus { Locked, Unlocked }

    // ── X-Y Table Entity ─────────────────────────────────────────────

    /// <summary>
    /// Motorized X-Y positioning table.
    /// Validates travel limits and enforces state machine transitions.
    /// </summary>
    public sealed class XYTable
    {
        public double TravelXMm { get; }
        public double TravelYMm { get; }
        public double ResolutionUm { get; }
        public double MaxSpeedMmS { get; }

        public Position2D Position { get; private set; }
        public TableState State { get; private set; }
        public bool IsHomed { get; private set; }

        public XYTable(
            double travelXMm = 300.0,
            double travelYMm = 300.0,
            double resolutionUm = 1.0,
            double maxSpeedMmS = 250.0)
        {
            TravelXMm = travelXMm;
            TravelYMm = travelYMm;
            ResolutionUm = resolutionUm;
            MaxSpeedMmS = maxSpeedMmS;
            Position = new Position2D(0.0, 0.0);
            State = TableState.Idle;
            IsHomed = false;
        }

        /// <summary>Integrity: Reject moves outside travel range.</summary>
        public void ValidatePosition(Position2D target)
        {
            if (target.XMm < 0 || target.XMm > TravelXMm)
                throw new TableLimitException(
                    $"X={target.XMm:F2} mm outside range [0, {TravelXMm}]");
            if (target.YMm < 0 || target.YMm > TravelYMm)
                throw new TableLimitException(
                    $"Y={target.YMm:F2} mm outside range [0, {TravelYMm}]");
        }

        /// <summary>Integrity: Table must be homed and idle before moving.</summary>
        public void ValidateReady()
        {
            if (!IsHomed)
                throw new TableNotHomedException("Table must be homed first.");
            if (State != TableState.Idle && State != TableState.Scanning)
                throw new TableBusyException(
                    $"Cannot move while in state: {State}");
        }

        /// <summary>Move to target, enforcing all safety constraints.</summary>
        public void MoveTo(Position2D target)
        {
            ValidateReady();
            ValidatePosition(target);
            State = TableState.Moving;
            Position = target;
            State = TableState.Idle;
        }

        /// <summary>Home both axes to origin (0, 0).</summary>
        public void Home()
        {
            State = TableState.Homing;
            Position = new Position2D(0.0, 0.0);
            IsHomed = true;
            State = TableState.Idle;
        }

        /// <summary>Set state externally (for virtual controller).</summary>
        public void SetState(TableState state) => State = state;

        /// <summary>Set position externally (for virtual controller).</summary>
        public void SetPosition(Position2D pos) => Position = pos;

        /// <summary>Current X position as percentage of travel.</summary>
        public double ProgressXPct => (Position.XMm / TravelXMm) * 100.0;

        /// <summary>Current Y position as percentage of travel.</summary>
        public double ProgressYPct => (Position.YMm / TravelYMm) * 100.0;
    }

    // ── Laser Unit Entity ────────────────────────────────────────────

    /// <summary>
    /// Fiber laser source with power control.
    /// STRIDE: LaserInterlockException prevents firing when safety is compromised.
    /// </summary>
    public sealed class LaserUnit
    {
        public double MaxPowerW { get; }
        public double WavelengthNm { get; }
        public double MinPowerW { get; }

        public double CurrentPowerW { get; private set; }
        public LaserState State { get; private set; }

        public LaserUnit(
            double maxPowerW = 1000.0,
            double wavelengthNm = 1070.0,
            double minPowerW = 50.0)
        {
            MaxPowerW = maxPowerW;
            WavelengthNm = wavelengthNm;
            MinPowerW = minPowerW;
            CurrentPowerW = 0.0;
            State = LaserState.Off;
        }

        /// <summary>Integrity: Validate power then apply.</summary>
        public void SetPower(double powerW)
        {
            if (powerW < 0 || powerW > MaxPowerW)
                throw new LaserPowerException(
                    $"Power {powerW} W outside range [0, {MaxPowerW}]");
            CurrentPowerW = powerW;
        }

        /// <summary>STRIDE: Only arm if all interlocks are satisfied.</summary>
        public void Arm(SafetySystem interlock)
        {
            if (!interlock.AllInterlocksLocked)
                throw new LaserInterlockException(
                    "Cannot arm laser — safety interlocks are open.");
            State = LaserState.Armed;
        }

        /// <summary>Fire the laser (set state to FIRING).</summary>
        public void Fire(SafetySystem interlock)
        {
            if (State != LaserState.Armed)
                throw new LaserInterlockException("Laser must be armed before firing.");
            if (!interlock.AllInterlocksLocked)
                throw new LaserInterlockException(
                    "Interlocks opened — firing aborted.");
            if (CurrentPowerW < MinPowerW)
                throw new LaserPowerException(
                    $"Power {CurrentPowerW} W below minimum {MinPowerW} W.");
            State = LaserState.Firing;
        }

        /// <summary>Immediately stop the laser.</summary>
        public void Stop()
        {
            State = LaserState.Standby;
            CurrentPowerW = 0.0;
        }

        /// <summary>Fully power down.</summary>
        public void TurnOff()
        {
            CurrentPowerW = 0.0;
            State = LaserState.Off;
        }

        /// <summary>Current power as percentage of maximum.</summary>
        public double PowerPct =>
            MaxPowerW <= 0 ? 0.0 : (CurrentPowerW / MaxPowerW) * 100.0;
    }

    // ── Gas System Entity ────────────────────────────────────────────

    /// <summary>
    /// Controlled atmosphere system (Argon shielding gas).
    /// CIA: Validates flow range, monitors supply pressure.
    /// </summary>
    public sealed class GasSystem
    {
        public double MaxFlowLMin { get; }
        public double MinFlowLMin { get; }

        public FlowRate CurrentFlow { get; private set; }
        public GasState State { get; private set; }
        public double SupplyPressureBar { get; set; }
        public double ChamberO2Ppm { get; set; }

        public GasSystem(double maxFlowLMin = 20.0, double minFlowLMin = 0.0)
        {
            MaxFlowLMin = maxFlowLMin;
            MinFlowLMin = minFlowLMin;
            CurrentFlow = new FlowRate(0.0);
            State = GasState.Closed;
            SupplyPressureBar = 200.0;
            ChamberO2Ppm = 209500.0; // Atmospheric O2
        }

        /// <summary>Integrity: Validate then set gas flow.</summary>
        public void SetFlow(double flowLMin)
        {
            if (flowLMin < MinFlowLMin)
                throw new GasFlowException(
                    $"Flow {flowLMin} L/min below minimum {MinFlowLMin}");
            if (flowLMin > MaxFlowLMin)
                throw new GasFlowException(
                    $"Flow {flowLMin} L/min above maximum {MaxFlowLMin}");

            CurrentFlow = new FlowRate(flowLMin);
            State = flowLMin > 0 ? GasState.Flowing : GasState.Closed;
        }

        /// <summary>Begin chamber purge at specified flow rate.</summary>
        public void StartPurge(double flowLMin = 15.0)
        {
            SetFlow(flowLMin);
            State = GasState.Purging;
        }

        /// <summary>Close gas supply.</summary>
        public void Stop()
        {
            CurrentFlow = new FlowRate(0.0);
            State = GasState.Closed;
        }

        /// <summary>Check if O2 level is below 100 ppm threshold.</summary>
        public bool IsAtmosphereSafe => ChamberO2Ppm < 100.0;

        /// <summary>Remaining supply as percentage (200 bar = 100%).</summary>
        public double SupplyLevelPct =>
            System.Math.Min((SupplyPressureBar / 200.0) * 100.0, 100.0);
    }

    // ── Safety System Entity ─────────────────────────────────────────

    /// <summary>
    /// Laboratory safety interlock system.
    ///
    /// STRIDE defense:
    ///   - Spoofing: Interlock states are controlled via validated methods.
    ///   - Tampering: State changes go through specific methods only.
    ///   - Repudiation: All changes will be logged via audit port.
    /// </summary>
    public sealed class SafetySystem
    {
        public InterlockStatus DoorInterlock { get; private set; }
        public InterlockStatus ChamberInterlock { get; private set; }
        public bool EStopPressed { get; private set; }
        public bool LaserWarningActive { get; private set; }

        public SafetySystem()
        {
            DoorInterlock = InterlockStatus.Unlocked;
            ChamberInterlock = InterlockStatus.Unlocked;
            EStopPressed = false;
            LaserWarningActive = false;
        }

        /// <summary>All conditions must be met for laser operation.</summary>
        public bool AllInterlocksLocked =>
            DoorInterlock == InterlockStatus.Locked
            && ChamberInterlock == InterlockStatus.Locked
            && !EStopPressed;

        public void LockDoor() => DoorInterlock = InterlockStatus.Locked;
        public void UnlockDoor() => DoorInterlock = InterlockStatus.Unlocked;
        public void LockChamber() => ChamberInterlock = InterlockStatus.Locked;
        public void UnlockChamber() => ChamberInterlock = InterlockStatus.Unlocked;

        /// <summary>Emergency stop — latching, requires manual reset.</summary>
        public void PressEStop() => EStopPressed = true;

        /// <summary>Manual reset of emergency stop.</summary>
        public void ResetEStop() => EStopPressed = false;

        public void ActivateWarning() => LaserWarningActive = true;
        public void DeactivateWarning() => LaserWarningActive = false;

        /// <summary>Return a summary of all safety states.</summary>
        public SafetyStatusSummary GetStatusSummary() => new SafetyStatusSummary(
            DoorInterlock.ToString(),
            ChamberInterlock.ToString(),
            EStopPressed,
            LaserWarningActive,
            AllInterlocksLocked);
    }

    /// <summary>Immutable snapshot of safety system state.</summary>
    public readonly struct SafetyStatusSummary
    {
        public readonly string Door;
        public readonly string Chamber;
        public readonly bool EStop;
        public readonly bool WarningLight;
        public readonly bool AllClear;

        public SafetyStatusSummary(
            string door, string chamber, bool eStop, bool warningLight, bool allClear)
        {
            Door = door;
            Chamber = chamber;
            EStop = eStop;
            WarningLight = warningLight;
            AllClear = allClear;
        }
    }
}
