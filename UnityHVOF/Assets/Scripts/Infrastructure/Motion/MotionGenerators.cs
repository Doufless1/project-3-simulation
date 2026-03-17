// ============================================================================
// Motion Generators — Raster, Spiral, and Linear trajectory generation.
//
// Pattern: Strategy — all implement IMotionGenerator.
// SRP: Each class generates exactly one pattern type.
//
// Port of: infrastructure/motion/raster_generator.py,
//          infrastructure/motion/spiral_generator.py,
//          infrastructure/motion/linear_generator.py
// ============================================================================

using System;
using System.Collections.Generic;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.Ports;

namespace HVOFSim.Infrastructure.Motion
{
    // ── Raster Generator ─────────────────────────────────────────────

    /// <summary>Generates a back-and-forth raster scan pattern.</summary>
    public sealed class RasterGenerator : IMotionGenerator
    {
        public Trajectory Generate(Dictionary<string, object> parameters)
        {
            double widthM = GetParam<double>(parameters, "width_m", 0.010);
            double heightM = GetParam<double>(parameters, "height_m", 0.010);
            double speedMS = GetParam<double>(parameters, "speed_m_s", 0.005);
            double lineSpacingM = GetParam<double>(parameters, "line_spacing_m", 0.001);
            double zDistance = GetParam<double>(parameters, "z_distance_m", 0.0);

            var time = new List<double>();
            var x = new List<double>();
            var y = new List<double>();
            var z = new List<double>();

            int nLines = Math.Max((int)(heightM / lineSpacingM), 1);
            double t = 0.0;

            for (int line = 0; line <= nLines; line++)
            {
                double yPos = line * lineSpacingM;
                if (yPos > heightM) yPos = heightM;

                double xStart = (line % 2 == 0) ? 0.0 : widthM;
                double xEnd = (line % 2 == 0) ? widthM : 0.0;

                // Start of line
                if (time.Count == 0)
                {
                    time.Add(t);
                    x.Add(xStart);
                    y.Add(yPos);
                    z.Add(zDistance);
                }

                // End of line
                double dt = widthM / speedMS;
                t += dt;
                time.Add(t);
                x.Add(xEnd);
                y.Add(yPos);
                z.Add(zDistance);

                // Move to next line (if not last)
                if (line < nLines)
                {
                    double nextY = Math.Min((line + 1) * lineSpacingM, heightM);
                    double stepDt = lineSpacingM / speedMS;
                    t += stepDt;
                    time.Add(t);
                    x.Add(xEnd);
                    y.Add(nextY);
                    z.Add(zDistance);
                }
            }

            return new Trajectory(time.ToArray(), x.ToArray(), y.ToArray(), z.ToArray());
        }

        private static T GetParam<T>(Dictionary<string, object> parameters, string key, T defaultValue)
        {
            if (parameters.TryGetValue(key, out var val))
                return (T)Convert.ChangeType(val, typeof(T));
            return defaultValue;
        }
    }

    // ── Spiral Generator ─────────────────────────────────────────────

    /// <summary>Generates an Archimedean spiral scan pattern.</summary>
    public sealed class SpiralGenerator : IMotionGenerator
    {
        public Trajectory Generate(Dictionary<string, object> parameters)
        {
            double radiusM = GetParam<double>(parameters, "radius_m", 0.005);
            double spacingM = GetParam<double>(parameters, "spacing_m", 0.001);
            double speedMS = GetParam<double>(parameters, "speed_m_s", 0.005);
            double zDistance = GetParam<double>(parameters, "z_distance_m", 0.0);

            int nTurns = Math.Max((int)(radiusM / spacingM), 1);
            int pointsPerTurn = 36;
            int totalPoints = nTurns * pointsPerTurn;

            var time = new double[totalPoints];
            var x = new double[totalPoints];
            var y = new double[totalPoints];
            var z = new double[totalPoints];

            double centerX = radiusM;
            double centerY = radiusM;
            double t = 0.0;

            for (int i = 0; i < totalPoints; i++)
            {
                double theta = 2.0 * Math.PI * i / pointsPerTurn;
                double r = spacingM * theta / (2.0 * Math.PI);
                if (r > radiusM) r = radiusM;

                x[i] = centerX + r * Math.Cos(theta);
                y[i] = centerY + r * Math.Sin(theta);
                z[i] = zDistance;

                if (i > 0)
                {
                    double ds = Math.Sqrt(
                        (x[i] - x[i - 1]) * (x[i] - x[i - 1]) +
                        (y[i] - y[i - 1]) * (y[i] - y[i - 1]));
                    t += ds / speedMS;
                }
                time[i] = t;
            }

            return new Trajectory(time, x, y, z);
        }

        private static T GetParam<T>(Dictionary<string, object> parameters, string key, T defaultValue)
        {
            if (parameters.TryGetValue(key, out var val))
                return (T)Convert.ChangeType(val, typeof(T));
            return defaultValue;
        }
    }

    // ── Linear Generator ─────────────────────────────────────────────

    /// <summary>Generates a simple straight-line scan trajectory.</summary>
    public sealed class LinearGenerator : IMotionGenerator
    {
        public Trajectory Generate(Dictionary<string, object> parameters)
        {
            double lengthM = GetParam<double>(parameters, "length_m", 0.010);
            double speedMS = GetParam<double>(parameters, "speed_m_s", 0.005);
            double zDistance = GetParam<double>(parameters, "z_distance_m", 0.0);
            int nPoints = GetParam<int>(parameters, "n_points", 100);

            if (nPoints < 2) nPoints = 2;

            double totalTime = lengthM / speedMS;

            var time = new double[nPoints];
            var x = new double[nPoints];
            var y = new double[nPoints];
            var z = new double[nPoints];

            for (int i = 0; i < nPoints; i++)
            {
                double frac = (double)i / (nPoints - 1);
                time[i] = frac * totalTime;
                x[i] = frac * lengthM;
                y[i] = 0.0;
                z[i] = zDistance;
            }

            return new Trajectory(time, x, y, z);
        }

        private static T GetParam<T>(Dictionary<string, object> parameters, string key, T defaultValue)
        {
            if (parameters.TryGetValue(key, out var val))
                return (T)Convert.ChangeType(val, typeof(T));
            return defaultValue;
        }
    }
}
