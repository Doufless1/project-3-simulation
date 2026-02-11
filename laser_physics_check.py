"""
Laser Physics Verification Script
Calculates power density and theoretical blackbody temperature limits.
"""

import numpy as np

def calculate_laser_physics(power_watts, spot_radius_meters):
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
    # This is the temp a blackbody would reach if it radiated ALL that power back.
    stefan_boltzmann = 5.670374419e-8
    max_temp_k = (irradiance_W_m2 / stefan_boltzmann)**0.25
    max_temp_c = max_temp_k - 273.15
    
    print(f"="*60)
    print(f"LASER PHYSICS CHECK: {power_watts}W @ {spot_radius_meters*1e6:.0f}µm Radius")
    print(f"="*60)
    print(f"Spot Area:        {area_cm2:.2e} cm²")
    print(f"Power Density:    {irradiance_MW_cm2:.2f} MW/cm²")
    print(f"Solar Equivalent: {suns/1e6:.1f} Million Suns")
    print(f"Theo. Max Temp:   {max_temp_c:.0f} °C (Blackbody Limit)")
    print(f"="*60)
    
    return max_temp_c

if __name__ == "__main__":
    # Our Simulation Parameters
    # 100W ND:YAG (cheap industrial)
    calculate_laser_physics(100.0, 50e-6)
    
    # Comparison: A standard laser pointer (5mW)
    # calculate_laser_physics(0.005, 500e-6)
