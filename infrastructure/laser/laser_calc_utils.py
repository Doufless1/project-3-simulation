"""
Laser Physics Verification Script
Calculates power density and theoretical blackbody temperature limits.
"""

import numpy as np


def calculate_laser_physics(power_watts, spot_radius_meters):
    """Calculate laser physics parameters. Returns a dict of results."""
    # 1. Geometry
    area_m2 = np.pi * (spot_radius_meters**2)
    area_cm2 = area_m2 * 10000

    # 2. Power Density (Irradiance)
    irradiance_W_m2 = power_watts / area_m2
    irradiance_MW_cm2 = (power_watts / 1e6) / area_cm2

    # 3. Comparison to Sun
    # Solar Constant roughly 1000 W/m2 (1 kW/m2)
    suns = irradiance_W_m2 / 1000.0

    # 4. Theoretical Blackbody Limit (Stefan-Boltzmann Law)
    # P/A = σ * T^4  =>  T = ( (P/A) / σ )^0.25
    stefan_boltzmann = 5.670374419e-8
    max_temp_k = (irradiance_W_m2 / stefan_boltzmann)**0.25
    max_temp_c = max_temp_k - 273.15

    return {
        "power_watts": power_watts,
        "spot_radius_m": spot_radius_meters,
        "area_cm2": area_cm2,
        "irradiance_MW_cm2": irradiance_MW_cm2,
        "solar_equivalent_million": suns / 1e6,
        "max_temp_celsius": max_temp_c,
    }


if __name__ == "__main__":
    # Our Simulation Parameters
    # 100W ND:YAG (cheap industrial)
    result = calculate_laser_physics(100.0, 50e-6)
    print(f"{'='*60}")
    print(f"LASER PHYSICS CHECK: {result['power_watts']}W @ "
          f"{result['spot_radius_m']*1e6:.0f}µm Radius")
    print(f"{'='*60}")
    print(f"Spot Area:        {result['area_cm2']:.2e} cm²")
    print(f"Power Density:    {result['irradiance_MW_cm2']:.2f} MW/cm²")
    print(f"Solar Equivalent: {result['solar_equivalent_million']:.1f} Million Suns")
    print(f"Theo. Max Temp:   {result['max_temp_celsius']:.0f} °C (Blackbody Limit)")
    print(f"{'='*60}")
