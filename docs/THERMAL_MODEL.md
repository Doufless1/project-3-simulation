# Thermal Model

The engine answers one question for the laboratory: **for a given laser, recipe and coating, does the surface remelt deeply enough to close porosity — without melting the substrate?** It does so with three interchangeable solvers behind the `IHeatSolver` port, plus a two-stage coating model.

| Solver | Dimensionality | Use | Source |
|---|---|---|---|
| `FDMHeatSolver3D` (`FDM-3D-FTCS`) | 3D transient | Full scan over a sample; fluence map, peak temperature, melt / vaporisation depth | [`fdm_heat_solver_3d.py`](../python/infrastructure/solvers/fdm_heat_solver_3d.py) · [`FDMHeatSolver3D.cs`](../UnityHVOF/Assets/Scripts/Infrastructure/Solvers/FDMHeatSolver3D.cs) |
| `AnalyticalHeatSolver` (`Analytical-1D`) | 1D closed form | Instant sanity check of the FDM result | [`analytical_solver.py`](../python/infrastructure/solvers/analytical_solver.py) |
| `FDMCoatingSolver2D` | 2D cross-section, pore-aware | Porosity closure, melt depth, HAZ, substrate safety | [`fdm_coating_solver_2d.py`](../python/infrastructure/solvers/fdm_coating_solver_2d.py) |

---

## 1. Beam model

Gaussian TEM₀₀ profile (default) or top-hat, selected through `ILaserSource`:

$$
I(x,y) = \frac{2P}{\pi w^2}\,\exp\!\left(-\frac{2r^2}{w^2}\right), \qquad r^2=(x-x_b)^2+(y-y_b)^2
$$

Defocus is handled by Gaussian beam propagation, so a Z offset in the trajectory widens the spot and lowers intensity:

$$
w(z) = w_0\sqrt{1+\left(\frac{z}{z_R}\right)^2}, \qquad z_R=\frac{\pi w_0^2}{\lambda}
$$

Only the absorbed fraction $\alpha I$ enters the solid (α ≈ 0.45 for WC-NiCr at 1 µm).

## 2. 3D transient conduction — `FDM-3D-FTCS`

$$
\frac{\partial T}{\partial t} = \kappa\,\nabla^2 T, \qquad \kappa=\frac{k}{\rho c_p}
$$

| Item | Implementation |
|---|---|
| Scheme | Explicit Forward-Time Central-Space, fully vectorised NumPy slicing (no Python loops over cells) |
| Stability | $\Delta t_{max}=\Delta x^2/(6\kappa)$ — the solver uses **80 %** of it, or the trajectory step if smaller, and sub-steps each trajectory segment |
| Top surface | Neumann flux $q=\alpha I(x,y)$ applied as $\Delta T = q\,\Delta t/(\rho c_p\,\Delta z)$ plus ghost-node conduction |
| Sides | Adiabatic (mirror) |
| Bottom | Adiabatic or fixed ambient (constructor switch) |
| Outputs | Final 3D field, cumulative fluence map $F=\int \alpha I\,dt$, peak T, melt depth, vaporisation depth, total absorbed energy, wall-clock time |
| Guard rails | Grid capped at 5 M cells (`GridTooLargeError`); non-positive Δt rejected (`SolverInstabilityError`) |

Default domain: 10 × 10 × 2 mm at 100 µm resolution.

## 3. Two-stage coating model — `HVOFLaserUseCase`

**Stage 1 — microstructure.** `HVOFCoatingGenerator` builds a 2D cross-section of coating on top of a steel substrate: lamellar splats (5–10 µm) with pores placed 3× more often on splat boundaries, matching a target porosity (3 % for the WC-NiCr reference). Pores take the conductivity of air (0.025 W/m·K) in the k-map, so they act as local hot spots — visible as isolated melted cells in the figure below.

**Stage 2 — remelting.** `FDMCoatingSolver2D` solves heterogeneous conduction with a moving, volumetrically absorbed source:

$$
\rho c_p\frac{\partial T}{\partial t}=\frac{\partial}{\partial x}\!\left(k\frac{\partial T}{\partial x}\right)+\frac{\partial}{\partial y}\!\left(k\frac{\partial T}{\partial y}\right)+Q
$$

$$
Q(x,y,t)=\frac{2\alpha P}{\pi r^2}\exp\!\left(-\frac{2(x-vt)^2}{r^2}\right)e^{-\mu y}
$$

Harmonic-mean face conductivities are precomputed outside the time loop; Δt is 40 % of the 2D explicit limit. Every cell that ever exceeds $T_{melt}$ is recorded in a cumulative melt mask.

**Post-processing** ([`coating_analysis.py`](../python/infrastructure/coating/coating_analysis.py)):

| Metric | Rule |
|---|---|
| Porosity after | Pores inside the melt mask are considered sealed |
| Melt depth | Deepest melted row |
| HAZ depth | Coating rows above 200 °C but not melted, measured from the melt front |
| Hardness | $HV_{after}=HV_{before}\,(1+\beta\,\Delta\phi/\phi_0)$, β = 0.3 (calibrated: 72 % porosity reduction → ≈ 21 % hardness gain) |
| Substrate safety | Flags any substrate cell above the steel melting point |

## 4. Validation against published data

[`run_hvof_validation.py`](../python/run_hvof_validation.py) sweeps 200 / 270 / 290 W (1 mm beam radius, 15 mm/s, 10 µm grid) on WC-NiCr and checks the best case against literature ranges:

| Metric | Simulated (290 W) | Published range | |
|---|---:|---:|:-:|
| Porosity reduction | 36.8 % | 30 – 72 % | ✅ |
| Melt depth | 100 µm | 50 – 150 µm | ✅ |
| Peak surface temperature | 1 955 °C | 1 400 – 2 000 °C | ✅ |
| HAZ depth | 180 µm | 100 – 200 µm | ✅ |
| Substrate melting | none | none | ✅ |

| Power | Peak T | Melt depth | Porosity Δ | HAZ | HV after |
|---:|---:|---:|---:|---:|---:|
| 200 W | 1 356 °C | 10 µm | 2.3 % | 270 µm | 1 007 |
| 270 W | 1 822 °C | 80 µm | 26.4 % | 200 µm | 1 079 |
| 290 W | 1 955 °C | 100 µm | 36.8 % | 180 µm | 1 110 |

The trend is the physically expected one: below ≈ 250 W the matrix barely melts; between 270 and 290 W the melt front crosses a third of the 300 µm coating while the substrate stays solid. The 290 W cross-section is rendered by [`tools/render_figures.py`](../python/tools/render_figures.py):

![WC-NiCr remelting cross-section](media/images/coating-remelting-290w.png)

Reproduce (≈ 40 s):

```bash
cd python
python run_hvof_validation.py
python tools/render_figures.py
```

## 5. Limitations

The model is a design tool, not a substitute for experiments:

- Constant material properties — no temperature dependence, latent heat or phase change.
- No melt-pool convection (Marangoni flow) or shielding-gas convection.
- The 2D coating model is a single cross-section; pores are sealed by a melt criterion, not by fluid flow.
- The hardness relation is empirical and calibrated on a single study.
- Validation is against literature ranges, not yet against samples processed in the CDAL laboratory.
