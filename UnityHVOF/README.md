# UnityHVOF — 3D digital twin

A physically-scaled, walkable model of the laser laboratory with a full operator dashboard. The room, enclosure, X-Y table and VFX are built procedurally from real dimensions; the control logic and thermal solver are a C# port of the Python domain model.

![Laser head over the X-Y table](../docs/media/images/unity-laser-xy-table.png)

## Requirements

- **Unity 6** — `6000.3.11f1` (see `ProjectSettings/ProjectVersion.txt`)
- Universal Render Pipeline 17.3, Input System 1.19, uGUI / TextMesh Pro, Test Framework 1.6 (resolved automatically from `Packages/manifest.json`)

## Run it

1. Unity Hub → **Add project from disk** → select this `UnityHVOF/` folder.
2. Open `Assets/important.unity` and press **Play**.
3. If the scene is empty, use **HVOF Tools → Attach Composition Root** — it creates the `SimulationEntryPoint` object that builds the whole lab at runtime.

| Control | Action |
|---|---|
| **F2** / *Hide* button | Toggle the dashboard to inspect the 3D scene |
| Right-drag or Alt + left-drag · arrow keys | Orbit |
| **W A S D** (+ **Shift**) | Fly (faster) |

Dashboard tabs: **Setup** (laser type, gas, chamber, material) · **X-Y Table** · **Laser & Gas** · **Safety** · **Cost**. A full cycle is described in [`docs/OPERATOR_WALKTHROUGH.md`](../docs/OPERATOR_WALKTHROUGH.md).

## Code layout

```text
Assets/Scripts/
├── Domain/          HVOFSim.Domain          — entities, value objects, ports  (no UnityEngine)
├── Application/     HVOFSim.Application     — LabControl / Simulation / HVOFLaser use cases  (no UnityEngine)
├── Infrastructure/  HVOFSim.Infrastructure  — FDM-3D & 2D solvers, beam sources, motion, virtual hardware, audit log
├── Presentation/    HVOFSim.Presentation    — CompositionRoot, MainDashboard (uGUI), LabSceneBuilder, OrbitCamera, LabVFX, HeatmapRenderer
├── Editor/          menu helper (HVOF Tools → Attach Composition Root)
└── Tests/           EditMode (domain, infrastructure, use cases) + PlayMode integration — 93 tests
```

Each folder is its own assembly definition; Domain and Application set `noEngineReferences: true`, so the compiler guarantees they stay engine-independent. See [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md).

## Tests

**Window → General → Test Runner** → *EditMode* → **Run All** (then *PlayMode*).

`Library/`, `Temp/`, `Logs/`, `UserSettings/` and generated `*.csproj` files are git-ignored; Unity regenerates them on first open.
