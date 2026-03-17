// ============================================================================
// Laser Factory — Creates laser source instances by name.
//
// Pattern: Factory — encapsulates creation logic.
// OCP: Add new profiles by adding cases, no modification to existing code.
//
// Port of: infrastructure/laser/laser_factory.py
// ============================================================================

using System;
using HVOFSim.Domain.Ports;

namespace HVOFSim.Infrastructure.Laser
{
    /// <summary>Factory for creating laser beam profile strategies.</summary>
    public static class LaserFactory
    {
        /// <summary>
        /// Create a laser source by profile name.
        /// </summary>
        /// <param name="profileName">"gaussian" or "tophat".</param>
        /// <returns>An ILaserSource implementation.</returns>
        public static ILaserSource Create(string profileName)
        {
            return profileName.ToLowerInvariant() switch
            {
                "gaussian" => new GaussianSource(),
                "tophat" => new TopHatSource(),
                _ => throw new ArgumentException(
                    $"Unknown laser profile '{profileName}'. " +
                    "Valid options: gaussian, tophat.")
            };
        }
    }
}
