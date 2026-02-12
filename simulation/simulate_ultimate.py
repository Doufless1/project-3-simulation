"""
Simulation of Laser Heating on ULTRA-C-Ta-WH-3849
The "Ultimate Material" discovered by the AI Engine.

Properties:
- Composition: Ta:22%, C:36%, W:19%, Cr:17%
- Melting Point: 3,376 °C (Verified)
- Hardness: 41,301 HV
- Density: 9,939 kg/m³

Estimated Thermal Properties (Rule of Mixtures + Cermet Physics):
- Thermal Conductivity: ~60 W/mK (mix of conductive W/Ta and resistive Carbides)
- Specific Heat: ~386 J/kgK
- Absorption: 0.70 (Dark grey carbide ceramic)
"""

import numpy as np
from .process_model import MaterialProperties, run_process_simulation
from .laser import nd_yag_laser
from .motion import raster_path

# 1. Define the Ultimate Material
ULTIMATE_MAT = MaterialProperties(
    name="ULTRA-C-Ta-WH-3849",
    T_melt=3376.0,          # °C
    T_vaporization=4800.0,  # °C (Est)
    thermal_conductivity=60.0, # W/mK
    specific_heat=386.0,       # J/kgK
    density=9939.0,            # kg/m³
    absorption=0.70            # High absorption (dark ceramic)
)

print(f"="*60)
print(f"SIMULATING: {ULTIMATE_MAT.name}")
print(f"="*60)
print(f"Properties:")
print(f"  Melting Point: {ULTIMATE_MAT.T_melt} °C")
print(f"  Vaporization:  {ULTIMATE_MAT.T_vaporization} °C")
print(f"  Density:       {ULTIMATE_MAT.density} kg/m³")
print(f"  Conductivity:  {ULTIMATE_MAT.thermal_conductivity} W/mK")

# 2. Configure High-Energy Laser
# Trying 100W @ 500 mm/s
params = {
    'power': 100.0,       # Watts
    'speed': 0.5,         # m/s (500 mm/s)
    'spot': 50e-6         # 50 µm radius
}

laser = nd_yag_laser(power=params['power'], spot_radius=params['spot'])
motion = raster_path(
    x_start=0.002, x_end=0.008,
    y_start=0.002, y_end=0.008,
    z_focus=0.0,
    line_spacing=50e-6, # Overlap for smooth coating
    scan_speed=params['speed'],
    dt=5e-5
)

print(f"\nLaser Parameters:")
print(f"  Power: {laser.power} W")
print(f"  Speed: {params['speed']*1000:.0f} mm/s")
print(f"  Spot:  {laser.base_spot_radius*1e6:.0f} µm")

# 3. Run Simulation
print("\nRunning thermal simulation...")
result = run_process_simulation(
    motion=motion,
    laser=laser,
    material=ULTIMATE_MAT,
    grid_size=(0.01, 0.01),
    resolution=25e-6, # High resolution
    compute_depth_model=True
)

# 4. Analyze Results
T_peak = result.depth_model.T_max
melt_depth = result.depth_model.melt_depth
vap_depth = result.depth_model.vaporization_depth

print(f"\nRESULTS:")
print(f"  Peak Surf Temp: {T_peak:.0f} °C")

if T_peak < ULTIMATE_MAT.T_melt:
    print(f"  STATUS: DID NOT MELT! (Target: {ULTIMATE_MAT.T_melt}°C)")
    print("  -> Material is too refractory! Increase Power or Slow Down.")
else:
    print(f"  STATUS: MELTED SUCCESSFULLY!")
    if melt_depth:
        print(f"  Melt Depth: {melt_depth*1e6:.1f} µm")
    
    if vap_depth:
        print(f"  [WARNING] Surface Vaporization Detected! Depth: {vap_depth*1e6:.1f} µm")
        print("  -> Reduce power to avoid material loss.")
    else:
        print("  [OK] No Vaporization.")

# 5. Physics Check
flux = result.fluence.peak_fluence
print(f"\nPhysics Check:")
print(f"  Peak Fluence: {flux/1e4:.1f} J/cm²")

# 6. Visualization
print("\nGenerating visualization plots...")
import matplotlib.pyplot as plt

# Plot 1: Fluence Map (The "Heat Map")
plt.figure(figsize=(10, 8))
plt.imshow(result.fluence.fluence_map.T, extent=[0, 10, 0, 10], origin='lower', cmap='inferno')
plt.colorbar(label='Fluence (J/m²)')
plt.title(f"Laser Energy Deposition: {ULTIMATE_MAT.name}\n(100W @ 500 mm/s)")
plt.xlabel("X Position (mm)")
plt.ylabel("Y Position (mm)")
plt.savefig("ultimate_fluence_map.png", dpi=150)
print("  Saved: ultimate_fluence_map.png")

# Plot 2: Depth Profile (Melt Depth)
if result.depth_model:
    dm = result.depth_model
    z_um = dm.depth * 1e6
    T_max_z = np.max(dm.temperature, axis=1) # Max temp at each depth
    
    plt.figure(figsize=(10, 6))
    plt.plot(z_um, T_max_z, 'r-', linewidth=2, label='Peak Temp')
    plt.axhline(ULTIMATE_MAT.T_melt, color='k', linestyle='--', label=f'Melt Point ({ULTIMATE_MAT.T_melt}°C)')
    plt.axvline(dm.melt_depth*1e6 if dm.melt_depth else 0, color='b', linestyle=':', label='Melt Depth')
    
    plt.fill_between(z_um, T_max_z, ULTIMATE_MAT.T_melt, where=(T_max_z >= ULTIMATE_MAT.T_melt), 
                     color='red', alpha=0.3, label='Melted Zone')
    
    plt.title(f"Melt Depth Profile: {ULTIMATE_MAT.name}")
    plt.xlabel("Depth (µm)")
    plt.ylabel("Peak Temperature (°C)")
    plt.xlim(0, 100) # Zoom in top 100um
    plt.grid(True, alpha=0.3)
    plt.legend()
    plt.savefig("ultimate_melt_profile.png", dpi=150)
    print("  Saved: ultimate_melt_profile.png")

print("Visualization Complete!")
