# Documentation

| Document | What's inside |
|---|---|
| [Architecture](ARCHITECTURE.md) | Clean Architecture layers, ports & adapters, compiler-enforced layering in Unity, end-to-end scan sequence, Python ↔ Unity parity, design decisions |
| [Thermal Model](THERMAL_MODEL.md) | Beam model, 3D FTCS solver, two-stage HVOF coating model, validation against published data, limitations |
| [Safety System](SAFETY_SYSTEM.md) | Device state machines, the four programmable safety rules, E-stop, STRIDE threat model, hardware measures, standards |
| [Lab Design & Cost](LAB_DESIGN_AND_COST.md) | Requirements, laser selection, room, X-Y table design and parts, €73.8k bill of materials |
| [Operator Walkthrough](OPERATOR_WALKTHROUGH.md) | A full treatment cycle in the Unity twin, screen by screen |

## Research notes

| | |
|---|---|
| [Midterm technical study](research/midterm-technical-study.md) | Literature review of HVOF spraying, laser post-treatment and alloy systems, plus the first-pass design (superseded by the documents above for final numbers) |

## Media

All figures live in [`media/images/`](media/images). Screenshots of the Unity build, the schematics and the cost charts were produced during the project; [`coating-remelting-290w.png`](media/images/coating-remelting-290w.png) is regenerated from the solver by [`python/tools/render_figures.py`](../python/tools/render_figures.py).
