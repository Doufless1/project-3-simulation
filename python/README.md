# Python — thermal engine, lab-control twin & dashboards

Pure-Python implementation of the laboratory: the domain model, the finite-difference thermal solvers, a virtual lab-control system that emits GRBL G-code, and three Dash web apps.

## Quick start

```bash
cd python
python -m venv .venv
# Windows: .venv\Scripts\activate    macOS/Linux: source .venv/bin/activate
pip install -r requirements-dev.txt
python -m pytest                       # 74 tests
```

| Run | Command | URL |
|---|---|---|
| Landing page (links both apps) | `python landing.py` | http://localhost:8049 |
| Simulation lab — solvers, materials, 3D results | `python run_gui.py` | http://localhost:8050 |
| Lab-control digital twin — table, laser, gas, safety, cost | `python lab_control/run_dashboard.py` | http://127.0.0.1:8051 |
| Literature validation sweep (≈ 40 s) | `python run_hvof_validation.py` | — |
| CLI simulation runner | `python -m presentation.cli_runner --help` | — |
| Regenerate documentation figures | `python tools/render_figures.py` | — |
| Regenerate dashboard icons | `python tools/generate_icons.py` | — |

## Layout

```text
python/
├── domain/             # Simulation entities, value objects, exceptions, ports (no dependencies)
├── application/        # SimulationUseCase, HVOFLaserUseCase (two-stage coating model)
├── infrastructure/
│   ├── solvers/        # FDM-3D-FTCS, analytical, 2D pore-aware coating solver
│   ├── laser/          # Gaussian / top-hat beam profiles, laser presets
│   ├── motion/         # Raster, spiral, linear trajectory generators
│   ├── coating/        # Microstructure generator + porosity / HAZ / hardness analysis
│   ├── persistence/    # JSON material repository, Materials Project adapter
│   └── logging/        # Audit logger (SHA-256 result hash, secret redaction)
├── presentation/       # Simulation-lab Dash app (web_ui/), CLI, plotting
├── lab_control/        # Self-contained lab-control bounded context
│   ├── domain/         # XYTable, LaserUnit, GasSystem, SafetySystem + FSMs
│   ├── application/    # LabControlUseCase — single entry point for every action
│   ├── infrastructure/ # Virtual table/laser/gas/safety controllers, cost catalogue
│   ├── presentation/   # Dash dashboard + Plotly 3D lab
│   └── tests/
├── discovery/          # Experimental material-screening scripts (optional mp-api)
├── shared/             # Design tokens shared by the Dash apps
├── tests/              # Simulation-engine tests
└── tools/              # Figure and icon generators
```

## Optional: Materials Project

`discovery/` and `infrastructure/persistence/materials_project_adapter.py` can query the [Materials Project](https://materialsproject.org) API. Install `mp-api` and `pymatgen`, then set `MP_API_KEY` in your environment. Everything else runs offline.

See [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md) and [`docs/THERMAL_MODEL.md`](../docs/THERMAL_MODEL.md) for the design and the physics.
