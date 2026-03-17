// ============================================================================
// Virtual Hardware Controllers — Simulated lab equipment for the digital twin.
//
// Pattern: Strategy — each implements a domain port interface.
// These simulate real hardware behavior with realistic delays and physics.
//
// Port of: lab_control/infrastructure/virtual_table_controller.py,
//          lab_control/infrastructure/virtual_laser_controller.py,
//          lab_control/infrastructure/virtual_gas_controller.py,
//          lab_control/infrastructure/virtual_safety_monitor.py
// ============================================================================

using System;
using System.Collections.Generic;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.Ports;
using HVOFSim.Domain.ValueObjects;

namespace HVOFSim.Infrastructure.VirtualHardware
{
    // ── Virtual Table Controller ─────────────────────────────────────

    /// <summary>
    /// Simulated X-Y table controller with G-code generation and scan execution.
    /// </summary>
    public sealed class VirtualTableController : ITableController
    {
        public void Home(XYTable table)
        {
            table.Home();
        }

        public void MoveTo(XYTable table, Position2D target)
        {
            table.MoveTo(target);
        }

        public List<Position2D> ExecuteScan(XYTable table, ScanRecipe recipe)
        {
            table.ValidateReady();
            table.SetState(TableState.Scanning);

            var path = new List<Position2D>();
            double lineSpacing = recipe.LineSpacingMm;
            int nLines = (int)(recipe.HeightMm / lineSpacing);

            for (int line = 0; line <= nLines; line++)
            {
                double y = line * lineSpacing;
                if (y > recipe.HeightMm) y = recipe.HeightMm;

                double xStart = (line % 2 == 0) ? 0 : recipe.WidthMm;
                double xEnd = (line % 2 == 0) ? recipe.WidthMm : 0;

                // Simulate line scan with intermediate points
                int pointsPerLine = 10;
                for (int p = 0; p <= pointsPerLine; p++)
                {
                    double frac = (double)p / pointsPerLine;
                    double x = xStart + frac * (xEnd - xStart);
                    var pos = new Position2D(x, y);
                    path.Add(pos);
                    table.SetPosition(pos);
                }
            }

            table.SetState(TableState.Idle);
            return path;
        }

        public List<GCodeLine> GenerateGCode(ScanRecipe recipe)
        {
            var gcode = new List<GCodeLine>();
            int lineNum = 0;

            gcode.Add(new GCodeLine("G28 ; Home all axes", lineNum++));
            gcode.Add(new GCodeLine($"G1 F{recipe.SpeedMmS * 60:F0} ; Set feed rate", lineNum++));

            double lineSpacing = recipe.LineSpacingMm;
            int nLines = (int)(recipe.HeightMm / lineSpacing);

            for (int line = 0; line <= nLines; line++)
            {
                double y = Math.Min(line * lineSpacing, recipe.HeightMm);
                double xEnd = (line % 2 == 0) ? recipe.WidthMm : 0;

                gcode.Add(new GCodeLine($"G1 X{xEnd:F3} Y{y:F3}", lineNum++));
            }

            gcode.Add(new GCodeLine("G28 ; Return home", lineNum++));
            return gcode;
        }
    }

    // ── Virtual Laser Controller ─────────────────────────────────────

    /// <summary>Simulated laser controller delegating to domain entity.</summary>
    public sealed class VirtualLaserController : ILaserController
    {
        public void SetPower(LaserUnit laser, double powerW)
        {
            laser.SetPower(powerW);
        }

        public void Arm(LaserUnit laser, SafetySystem safety)
        {
            laser.Arm(safety);
        }

        public void Fire(LaserUnit laser, SafetySystem safety)
        {
            laser.Fire(safety);
        }

        public void Stop(LaserUnit laser)
        {
            laser.Stop();
        }
    }

    // ── Virtual Gas Controller ────────────────────────────────────────

    /// <summary>
    /// Simulated gas controller with O2 decay during purge.
    /// </summary>
    public sealed class VirtualGasController : IGasController
    {
        private const double O2DecayRate = 0.85; // Per update cycle

        public void SetFlow(GasSystem gas, double flowLMin)
        {
            gas.SetFlow(flowLMin);
        }

        public void StartPurge(GasSystem gas)
        {
            gas.StartPurge();
        }

        public void Stop(GasSystem gas)
        {
            gas.Stop();
        }

        public double ReadO2(GasSystem gas)
        {
            // Simulate O2 decay when purging
            if (gas.State == GasState.Purging || gas.State == GasState.Flowing)
            {
                gas.ChamberO2Ppm *= O2DecayRate;
                if (gas.ChamberO2Ppm < 10) gas.ChamberO2Ppm = 10;
            }
            return gas.ChamberO2Ppm;
        }
    }

    // ── Virtual Safety Monitor ────────────────────────────────────────

    /// <summary>Simulated safety monitor delegating to domain entity.</summary>
    public sealed class VirtualSafetyMonitor : ISafetyMonitor
    {
        public SafetyStatusSummary CheckAll(SafetySystem safety)
        {
            return safety.GetStatusSummary();
        }

        public void TriggerEStop(SafetySystem safety)
        {
            safety.PressEStop();
        }

        public void ResetEStop(SafetySystem safety)
        {
            safety.ResetEStop();
        }
    }
}
