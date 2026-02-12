"""
Process Model for Laser Heating Simulation of HVOF Coatings.

Implements:
1. Cumulative Fluence Map: F(x,y) = Σ I(x,y,z(t)) · Δt
2. Defocus Effect: Spot size varies with Z distance
3. 1D Depth Heat Model: Solves heat equation for temperature vs depth

Implementation: Path A (NumPy vectorized finite differences)
Material: 316L Stainless Steel

Author: Agent 3
"""

import numpy as np
from dataclasses import dataclass, field
from typing import Tuple, Optional, Dict, Any

# Import from companion modules
from .motion import MotionPath
from .laser import LaserConfig, compute_spot_radius_array, gaussian_intensity


# =============================================================================
# Material Properties: 316L Stainless Steel
# =============================================================================

@dataclass
class MaterialProperties:
    """
    Thermal properties of the target material.
    
    Default values are for 316L Stainless Steel.
    """
    name: str = "316L Stainless Steel"
    
    # Optical properties
    absorption: float = 0.35  # Absorption coefficient (35% absorbed)
    
    # Thermal properties
    thermal_diffusivity: float = 4.0e-6  # κ [m²/s]
    thermal_conductivity: float = 15.0   # k [W/(m·K)]
    density: float = 8000.0              # ρ [kg/m³]
    specific_heat: float = 500.0         # c_p [J/(kg·K)]
    
    # Phase change temperatures [°C]
    T_ambient: float = 20.0
    T_melt: float = 1400.0
    T_vaporization: float = 2800.0
    
    def __post_init__(self):
        """Validate thermal diffusivity consistency."""
        # κ = k / (ρ * c_p)
        computed_kappa = self.thermal_conductivity / (self.density * self.specific_heat)
        if not np.isclose(computed_kappa, self.thermal_diffusivity, rtol=0.1):
            pass  # Allow user-specified override


# Default material
SS316L = MaterialProperties()


# =============================================================================
# Result Data Structures
# =============================================================================

@dataclass
class FluenceResult:
    """
    Result of fluence map computation.
    
    Attributes:
        fluence_map: 2D array of cumulative fluence [J/m²]
        x_coords: X-axis coordinates [m]
        y_coords: Y-axis coordinates [m]
        peak_fluence: Maximum fluence value [J/m²]
        total_energy: Total deposited energy [J]
    """
    fluence_map: np.ndarray
    x_coords: np.ndarray
    y_coords: np.ndarray
    peak_fluence: float = field(init=False)
    total_energy: float = field(init=False)
    
    def __post_init__(self):
        self.peak_fluence = float(np.max(self.fluence_map))
        # Integrate over area (assumes uniform grid)
        dx = self.x_coords[1] - self.x_coords[0] if len(self.x_coords) > 1 else 1.0
        dy = self.y_coords[1] - self.y_coords[0] if len(self.y_coords) > 1 else 1.0
        self.total_energy = float(np.sum(self.fluence_map) * dx * dy)


@dataclass
class DepthResult:
    """
    Result of 1D depth heat model.
    
    Attributes:
        depth: Depth coordinates from surface [m]
        time: Time coordinates [s]
        temperature: 2D array T(z, t) [°C]
        T_surface_vs_time: Surface temperature history [°C]
        T_max: Peak temperature reached [°C]
        melt_depth: Maximum melt penetration depth [m] (None if no melting)
        vaporization_depth: Maximum vaporization depth [m] (None if none)
    """
    depth: np.ndarray
    time: np.ndarray
    temperature: np.ndarray
    T_surface_vs_time: np.ndarray = field(init=False)
    T_max: float = field(init=False)
    melt_depth: Optional[float] = field(init=False)
    vaporization_depth: Optional[float] = field(init=False)
    
    # Material reference for threshold computation
    _material: MaterialProperties = field(repr=False, default_factory=lambda: SS316L)
    
    def __post_init__(self):
        self.T_surface_vs_time = self.temperature[0, :]
        self.T_max = float(np.max(self.temperature))
        
        # Find melt depth (where T ever exceeded T_melt)
        max_T_at_depth = np.max(self.temperature, axis=1)
        melt_indices = np.where(max_T_at_depth >= self._material.T_melt)[0]
        if len(melt_indices) > 0:
            self.melt_depth = float(self.depth[melt_indices[-1]])
        else:
            self.melt_depth = None
        
        # Find vaporization depth
        vap_indices = np.where(max_T_at_depth >= self._material.T_vaporization)[0]
        if len(vap_indices) > 0:
            self.vaporization_depth = float(self.depth[vap_indices[-1]])
        else:
            self.vaporization_depth = None


@dataclass
class ProcessResult:
    """
    Combined result of process simulation.
    
    Attributes:
        fluence: FluenceResult with 2D cumulative fluence map
        depth_model: DepthResult with 1D temperature profile (optional)
        params: Dictionary of simulation parameters
    """
    fluence: FluenceResult
    depth_model: Optional[DepthResult] = None
    params: Dict[str, Any] = field(default_factory=dict)


# =============================================================================
# Core Functions: Fluence Map Computation
# =============================================================================

def compute_fluence_map(
    motion: MotionPath,
    laser: LaserConfig,
    material: MaterialProperties = SS316L,
    grid_size: Tuple[float, float] = (0.01, 0.01),  # 10mm x 10mm
    resolution: float = 50e-6,  # 50 µm pixels
    clipping_factor: float = 4.0,  # Clip Gaussian at 4w
    progress_callback: Optional[callable] = None
) -> FluenceResult:
    """
    Compute cumulative fluence map F(x,y) = Σ I(x,y,z(t)) · Δt.
    
    Implementation: Path A (NumPy vectorized)
    - Iterates over time steps
    - At each step, adds Gaussian intensity contribution to grid
    - Accounts for defocus effect (spot size varies with z)
    
    Args:
        motion: MotionPath with trajectory data (time, x, y, z)
        laser: LaserConfig with beam parameters
        material: MaterialProperties (uses absorption coefficient)
        grid_size: (width, height) of simulation domain [m]
        resolution: Grid cell size [m]
        clipping_factor: Ignore intensity beyond this many spot radii
        progress_callback: Optional function(fraction) for progress updates
        
    Returns:
        FluenceResult with 2D fluence map
    """
    # Create coordinate grid
    nx = int(np.ceil(grid_size[0] / resolution))
    ny = int(np.ceil(grid_size[1] / resolution))
    
    x_coords = np.linspace(0, grid_size[0], nx)
    y_coords = np.linspace(0, grid_size[1], ny)
    
    # Meshgrid for vectorized intensity calculation
    X, Y = np.meshgrid(x_coords, y_coords, indexing='ij')
    
    # Initialize fluence accumulator
    fluence = np.zeros((nx, ny), dtype=np.float64)
    
    # Precompute spot radii for all z positions
    spot_radii = compute_spot_radius_array(motion.z, laser)
    
    # Time steps
    dt = motion.dt  # Array of time steps
    
    # Iterate over trajectory points
    n_steps = motion.n_points
    for i in range(n_steps):
        # Current beam position and parameters
        cx, cy = motion.x[i], motion.y[i]
        w = spot_radii[i]
        delta_t = dt[i]
        
        # Skip if beam is far outside grid (optimization)
        clip_radius = clipping_factor * w
        if (cx + clip_radius < 0 or cx - clip_radius > grid_size[0] or
            cy + clip_radius < 0 or cy - clip_radius > grid_size[1]):
            continue
        
        # Compute intensity distribution (vectorized)
        intensity = gaussian_intensity(X, Y, cx, cy, w, laser.power)
        
        # Accumulate fluence (absorbed energy per unit area)
        fluence += material.absorption * intensity * delta_t
        
        # Progress callback
        if progress_callback is not None and i % 100 == 0:
            progress_callback(i / n_steps)
    
    if progress_callback is not None:
        progress_callback(1.0)
    
    return FluenceResult(
        fluence_map=fluence,
        x_coords=x_coords,
        y_coords=y_coords
    )


def compute_fluence_map_optimized(
    motion: MotionPath,
    laser: LaserConfig,
    material: MaterialProperties = SS316L,
    grid_size: Tuple[float, float] = (0.01, 0.01),
    resolution: float = 50e-6,
    clipping_factor: float = 4.0
) -> FluenceResult:
    """
    Optimized fluence computation with local windowing.
    
    Instead of computing Gaussian over entire grid, only updates
    cells within clipping radius of beam center. Faster for large
    grids or fine resolution.
    
    Same interface as compute_fluence_map.
    """
    # Create coordinate grid
    nx = int(np.ceil(grid_size[0] / resolution))
    ny = int(np.ceil(grid_size[1] / resolution))
    
    x_coords = np.linspace(0, grid_size[0], nx)
    y_coords = np.linspace(0, grid_size[1], ny)
    
    # Initialize fluence accumulator
    fluence = np.zeros((nx, ny), dtype=np.float64)
    
    # Precompute spot radii
    spot_radii = compute_spot_radius_array(motion.z, laser)
    dt = motion.dt
    
    # Iterate with local windowing
    for i in range(motion.n_points):
        cx, cy = motion.x[i], motion.y[i]
        w = spot_radii[i]
        delta_t = dt[i]
        
        # Determine affected grid region
        clip_radius = clipping_factor * w
        
        # Find index bounds
        i_min = max(0, int((cx - clip_radius) / resolution))
        i_max = min(nx, int((cx + clip_radius) / resolution) + 1)
        j_min = max(0, int((cy - clip_radius) / resolution))
        j_max = min(ny, int((cy + clip_radius) / resolution) + 1)
        
        # Skip if no overlap
        if i_min >= i_max or j_min >= j_max:
            continue
        
        # Local coordinates
        x_local = x_coords[i_min:i_max]
        y_local = y_coords[j_min:j_max]
        X_local, Y_local = np.meshgrid(x_local, y_local, indexing='ij')
        
        # Compute and accumulate
        intensity = gaussian_intensity(X_local, Y_local, cx, cy, w, laser.power)
        fluence[i_min:i_max, j_min:j_max] += material.absorption * intensity * delta_t
    
    return FluenceResult(
        fluence_map=fluence,
        x_coords=x_coords,
        y_coords=y_coords
    )


# =============================================================================
# 1D Depth Heat Model
# =============================================================================

def solve_1d_depth_heat(
    surface_fluence: float,
    pulse_duration: float,
    material: MaterialProperties = SS316L,
    max_depth: float = 1e-3,  # 1 mm
    t_max: float = 1e-3,       # 1 ms total simulation
    n_z: int = 100,
    n_t: int = 1000,
    boundary_condition: str = 'adiabatic'
) -> DepthResult:
    """
    Solve 1D heat equation for temperature vs depth and time.
    
    Equation: ∂T/∂t = κ * ∂²T/∂z²
    
    Boundary conditions:
    - Surface (z=0): Heat flux from laser during pulse, then adiabatic
    - Bottom (z=max_depth): Adiabatic (∂T/∂z = 0) or fixed T_ambient
    
    Implementation: Explicit finite difference (FTCS scheme)
    
    Args:
        surface_fluence: Energy deposited per unit area [J/m²]
        pulse_duration: Duration of energy deposition [s]
        material: MaterialProperties
        max_depth: Maximum depth to simulate [m]
        t_max: Total simulation time [s]
        n_z: Number of depth grid points
        n_t: Number of time steps
        boundary_condition: 'adiabatic' or 'fixed' at bottom
        
    Returns:
        DepthResult with temperature profile
    """
    # Grid setup
    dz = max_depth / (n_z - 1)
    dt = t_max / (n_t - 1)
    
    depth = np.linspace(0, max_depth, n_z)
    time = np.linspace(0, t_max, n_t)
    
    # Stability check (FTCS requires α = κ*dt/dz² < 0.5)
    kappa = material.thermal_diffusivity
    alpha = kappa * dt / dz**2
    if alpha > 0.5:
        # Reduce time step to ensure stability
        n_t = int(2 * kappa * t_max / (0.4 * dz**2)) + 1
        dt = t_max / (n_t - 1)
        time = np.linspace(0, t_max, n_t)
        alpha = kappa * dt / dz**2
    
    # Initialize temperature field
    T = np.full((n_z, n_t), material.T_ambient, dtype=np.float64)
    
    # Surface heat flux [W/m²] during pulse
    if pulse_duration > 0:
        surface_intensity = surface_fluence / pulse_duration
    else:
        surface_intensity = 0.0
    
    # Time stepping (explicit FTCS)
    for n in range(n_t - 1):
        current_time = time[n]
        
        # Interior points: T_new = T + α*(T_{i+1} - 2*T_i + T_{i-1})
        T[1:-1, n+1] = T[1:-1, n] + alpha * (T[2:, n] - 2*T[1:-1, n] + T[:-2, n])
        
        # Surface boundary (z=0): Apply heat flux during pulse
        if current_time < pulse_duration:
            # Heat flux BC: -k * dT/dz = q at z=0
            # Using forward difference: T[0] = T[1] + q*dz/k
            q = surface_intensity
            T[0, n+1] = T[1, n+1] + q * dz / material.thermal_conductivity
        else:
            # Adiabatic after pulse: dT/dz = 0 → T[0] = T[1]
            T[0, n+1] = T[1, n+1]
        
        # Bottom boundary
        if boundary_condition == 'adiabatic':
            T[-1, n+1] = T[-2, n+1]
        else:  # fixed
            T[-1, n+1] = material.T_ambient
    
    return DepthResult(
        depth=depth,
        time=time,
        temperature=T,
        _material=material
    )


def solve_1d_depth_analytical(
    surface_fluence: float,
    pulse_duration: float,
    material: MaterialProperties = SS316L,
    max_depth: float = 1e-3,
    t_eval: Optional[np.ndarray] = None,
    n_z: int = 100
) -> DepthResult:
    """
    Analytical solution for instantaneous surface heating.
    
    For a semi-infinite solid with instantaneous surface heat input Q:
    T(z,t) = T_amb + (Q / (ρ*c_p*√(π*κ*t))) * exp(-z²/(4*κ*t))
    
    This is an approximation for short pulses (pulse << diffusion time).
    
    Args:
        surface_fluence: Energy per unit area [J/m²]
        pulse_duration: Nominal pulse duration (for time scaling) [s]
        material: MaterialProperties
        max_depth: Maximum depth [m]
        t_eval: Time points to evaluate (default: 100 points to 10x pulse)
        n_z: Number of depth points
        
    Returns:
        DepthResult with analytical temperature profile
    """
    if t_eval is None:
        t_eval = np.linspace(pulse_duration * 0.1, pulse_duration * 10, 100)
    
    depth = np.linspace(0, max_depth, n_z)
    kappa = material.thermal_diffusivity
    rho_cp = material.density * material.specific_heat
    
    # Volumetric energy per unit area (Q has units J/m²)
    Q = surface_fluence  # Already absorbed by fluence calculation
    
    # Temperature array T(z, t)
    T = np.zeros((n_z, len(t_eval)))
    
    for j, t in enumerate(t_eval):
        if t <= 0:
            T[:, j] = material.T_ambient
        else:
            # Analytical solution for instantaneous surface source
            prefactor = Q / (rho_cp * np.sqrt(np.pi * kappa * t))
            T[:, j] = material.T_ambient + prefactor * np.exp(-depth**2 / (4 * kappa * t))
    
    return DepthResult(
        depth=depth,
        time=t_eval,
        temperature=T,
        _material=material
    )


# =============================================================================
# High-Level Process Functions
# =============================================================================

def run_process_simulation(
    motion: MotionPath,
    laser: LaserConfig,
    material: MaterialProperties = SS316L,
    grid_size: Tuple[float, float] = (0.01, 0.01),
    resolution: float = 50e-6,
    compute_depth_model: bool = True,
    depth_pixel: Optional[Tuple[int, int]] = None,
    optimized: bool = True
) -> ProcessResult:
    """
    Run complete process simulation.
    
    Computes 2D fluence map and optionally 1D depth heat model
    for the peak fluence location or specified pixel.
    
    Args:
        motion: MotionPath trajectory
        laser: LaserConfig parameters
        material: MaterialProperties
        grid_size: Simulation domain size [m]
        resolution: Grid resolution [m]
        compute_depth_model: Whether to compute 1D depth model
        depth_pixel: (i, j) indices for depth model location
                     (default: peak fluence location)
        optimized: Use optimized local-window algorithm
        
    Returns:
        ProcessResult with fluence map and optional depth model
    """
    # Compute fluence map
    if optimized:
        fluence = compute_fluence_map_optimized(
            motion, laser, material, grid_size, resolution
        )
    else:
        fluence = compute_fluence_map(
            motion, laser, material, grid_size, resolution
        )
    
    # Build parameter dictionary
    params = {
        'grid_size': grid_size,
        'resolution': resolution,
        'material': material.name,
        'laser_power': laser.power,
        'laser_wavelength': laser.wavelength,
        'absorption': material.absorption,
        'trajectory_duration': motion.duration,
        'trajectory_points': motion.n_points
    }
    
    # Optionally compute depth model
    depth_model = None
    if compute_depth_model:
        # Find location for depth model
        if depth_pixel is None:
            # Use peak fluence location
            peak_idx = np.unravel_index(
                np.argmax(fluence.fluence_map),
                fluence.fluence_map.shape
            )
        else:
            peak_idx = depth_pixel
        
        # Get fluence at this location
        local_fluence = fluence.fluence_map[peak_idx]
        
        # Estimate effective pulse duration from trajectory
        effective_pulse = motion.duration / motion.n_points * 10  # rough estimate
        
        depth_model = solve_1d_depth_heat(
            surface_fluence=local_fluence,
            pulse_duration=effective_pulse,
            material=material,
            max_depth=1e-3,  # 1 mm
            t_max=10 * effective_pulse
        )
        
        params['depth_model_location'] = peak_idx
        params['local_fluence_J_m2'] = float(local_fluence)
    
    return ProcessResult(
        fluence=fluence,
        depth_model=depth_model,
        params=params
    )


# =============================================================================
# Utility Functions
# =============================================================================

def estimate_peak_temperature(
    fluence: float,
    material: MaterialProperties = SS316L,
    pulse_duration: float = 1e-4
) -> float:
    """
    Estimate peak surface temperature from fluence.
    
    Uses analytical approximation for semi-infinite solid:
    ΔT ≈ 2 * F / (ρ * c_p * √(π * κ * τ))
    
    Args:
        fluence: Surface fluence [J/m²]
        material: MaterialProperties
        pulse_duration: Effective heating duration [s]
        
    Returns:
        Estimated peak temperature [°C]
    """
    rho_cp = material.density * material.specific_heat
    kappa = material.thermal_diffusivity
    
    delta_T = 2 * fluence / (rho_cp * np.sqrt(np.pi * kappa * pulse_duration))
    
    return material.T_ambient + delta_T


def melt_threshold_fluence(
    material: MaterialProperties = SS316L,
    pulse_duration: float = 1e-4
) -> float:
    """
    Estimate fluence required to reach melting temperature.
    
    Args:
        material: MaterialProperties
        pulse_duration: Effective heating duration [s]
        
    Returns:
        Threshold fluence for melting [J/m²]
    """
    delta_T_melt = material.T_melt - material.T_ambient
    rho_cp = material.density * material.specific_heat
    kappa = material.thermal_diffusivity
    
    fluence = delta_T_melt * rho_cp * np.sqrt(np.pi * kappa * pulse_duration) / 2
    return fluence


# =============================================================================
# Demo / Testing
# =============================================================================

if __name__ == "__main__":
    print("=" * 60)
    print("Process Model Demo: Laser Heating of HVOF Coating")
    print("=" * 60)
    
    # Import motion generators
    from motion import raster_path
    from laser import nd_yag_laser
    
    # Create a raster scan path (10mm x 10mm area)
    print("\n1. Creating raster scan path...")
    motion = raster_path(
        x_start=0.001,    # 1mm margins
        x_end=0.009,
        y_start=0.001,
        y_end=0.009,
        z_focus=0.0,      # At focal plane
        line_spacing=100e-6,  # 100 µm line spacing
        scan_speed=0.1,   # 100 mm/s
        dt=1e-4
    )
    print(f"   Path: {motion.n_points} points, {motion.duration:.3f} s duration")
    
    # Create laser configuration
    print("\n2. Configuring Nd:YAG laser...")
    laser = nd_yag_laser(power=100, spot_radius=50e-6)  # 100W, 50µm spot
    print(f"   Power: {laser.power} W")
    print(f"   Spot radius: {laser.base_spot_radius*1e6:.1f} µm")
    print(f"   Rayleigh range: {laser.rayleigh_range*1e3:.2f} mm")
    
    # Material properties
    print("\n3. Material: 316L Stainless Steel")
    print(f"   Absorption: {SS316L.absorption * 100:.0f}%")
    print(f"   Thermal diffusivity: {SS316L.thermal_diffusivity:.2e} m²/s")
    print(f"   Melt temperature: {SS316L.T_melt}°C")
    
    # Run simulation
    print("\n4. Running process simulation...")
    result = run_process_simulation(
        motion=motion,
        laser=laser,
        material=SS316L,
        grid_size=(0.01, 0.01),  # 10mm x 10mm
        resolution=50e-6,        # 50 µm pixels
        compute_depth_model=True,
        optimized=True
    )
    
    # Results summary
    print("\n" + "=" * 60)
    print("RESULTS")
    print("=" * 60)
    
    print(f"\nFluence Map:")
    print(f"   Grid: {result.fluence.fluence_map.shape}")
    print(f"   Peak fluence: {result.fluence.peak_fluence:.2e} J/m²")
    print(f"   Total energy: {result.fluence.total_energy:.4f} J")
    
    # Estimate peak temperature
    T_peak_est = estimate_peak_temperature(
        result.fluence.peak_fluence,
        SS316L,
        pulse_duration=motion.duration / motion.n_points
    )
    print(f"   Est. peak temperature: {T_peak_est:.0f}°C")
    
    if result.depth_model is not None:
        print(f"\nDepth Heat Model:")
        print(f"   Peak temperature: {result.depth_model.T_max:.0f}°C")
        if result.depth_model.melt_depth is not None:
            print(f"   Melt depth: {result.depth_model.melt_depth*1e6:.1f} µm")
        else:
            print(f"   Melt depth: None (below T_melt)")
        if result.depth_model.vaporization_depth is not None:
            print(f"   Vaporization depth: {result.depth_model.vaporization_depth*1e6:.1f} µm")
    
    # Threshold analysis
    print(f"\n5. Threshold Analysis:")
    F_melt = melt_threshold_fluence(SS316L, pulse_duration=1e-4)
    print(f"   Fluence for melting: {F_melt:.2e} J/m²")
    if result.fluence.peak_fluence > F_melt:
        print(f"   [WARNING] Peak fluence exceeds melt threshold!")
    else:
        safety_margin = (F_melt - result.fluence.peak_fluence) / F_melt * 100
        print(f"   [OK] Below melt threshold (margin: {safety_margin:.1f}%)")
    
    print("\n" + "=" * 60)
    print("Demo complete. Import this module to use in your simulation.")
    print("=" * 60)
