# Studying and Designing a Laser Treatment Laboratory to Improve HVOF Coatings

## PART 1: Literature Review & Technical Foundation

---

### 1.1 HVOF Technology — Comprehensive Review

#### 1.1.1 Process Description

High-Velocity Oxygen-Fuel (HVOF) spraying is a thermal spray technique where a fuel (gaseous or liquid) is combusted with oxygen in a combustion chamber. The burned gases are accelerated through a converging-diverging (de Laval) nozzle to reach supersonic velocities (Mach 1.5–3.0). Powder feedstock is injected into this high-velocity gas stream, where particles are heated to a semi-molten state and accelerated to 400–800 m/s before impacting the substrate.

#### 1.1.2 HVOF System Components

| Component | Function | Typical Specification |
|-----------|----------|----------------------|
| **Combustion Chamber** | Fuel/oxygen mixing and ignition | Pressure: 3–10 bar |
| **De Laval Nozzle** | Gas acceleration to supersonic velocity | Barrel length: 100–300 mm |
| **Powder Feeder** | Controlled powder delivery | Feed rate: 30–60 g/min |
| **Gas Supply System** | Fuel and oxygen delivery | O₂: 100–920 L/min |
| **Cooling System** | Prevents gun overheating | Water-cooled jacket |
| **Control Console** | Process parameter management | Digital PLC-based |
| **Robotic Manipulator** | Gun positioning and trajectory | 6-axis industrial robot |

#### 1.1.3 Process Parameters

| Parameter | Typical Range | Optimal Value | Effect |
|-----------|--------------|---------------|--------|
| **Oxygen flow rate** | 100–920 L/min | 240–270 L/min (gas fuel) | Controls flame stoichiometry |
| **Fuel flow rate (kerosene)** | 20–83 L/min | 52–68 L/min | Controls flame temperature |
| **Fuel flow rate (propane)** | 100–250 L/min | 150–200 L/min | Lower temperature than kerosene |
| **Spray distance** | 100–350 mm | 200–250 mm | Particle temperature at impact |
| **Powder feed rate** | 30–60 g/min | 35–50 g/min | Coating thickness per pass |
| **Traverse speed** | 100–1000 mm/s | 300–500 mm/s | Coating uniformity |
| **Number of passes** | 5–50 | Material-dependent | Total coating thickness |
| **Flame temperature** | 2800–3000 °C | — | Lower than plasma spray |
| **Particle velocity** | 400–800 m/s | 600+ m/s | Coating density |
| **Deposition rate** | 4–12 kg/h | Material-dependent | Production throughput |

#### 1.1.4 HVOF vs Other Thermal Spray Methods

| Property | HVOF | APS (Plasma) | Cold Spray | D-Gun | Wire Arc |
|----------|------|-------------|------------|-------|----------|
| **Particle velocity (m/s)** | 400–800 | 200–400 | 300–1200 | 500–1000 | 100–250 |
| **Flame/jet temperature (°C)** | 2800–3000 | 10,000–15,000 | 0–800 | 3000–4000 | 4000–6000 |
| **Porosity (%)** | <1–2 | 3–10 | <1 | <1–2 | 5–15 |
| **Bond strength (MPa)** | >70 | 30–70 | >70 | >70 | 15–40 |
| **Oxide content (%)** | 0.5–2 | 1–5 | <0.1 | 0.5–3 | 5–15 |
| **Coating thickness (µm)** | 50–500 | 50–2000 | 50–3000 | 50–500 | 100–5000 |
| **Decarburization** | Low | High | None | Low | N/A |
| **Cost** | Medium-High | Medium | High | High | Low |
| **Best for** | Carbides, metals | Ceramics | Cold-sensitive | Hard coatings | Large areas |

#### 1.1.5 HVOF Coating Defects (The Problem)

| Defect | Cause | Effect on Performance |
|--------|-------|----------------------|
| **Microporosity** | Incomplete particle deformation | Reduced corrosion resistance, pathways for corrosive media |
| **Oxide inclusions** | In-flight oxidation of particles | Reduced cohesive strength, crack initiation sites |
| **Partially melted particles** | Insufficient particle heating | Poor inter-splat bonding, increased porosity |
| **Inter-lamellar boundaries** | Splat-by-splat buildup | Weak planes, preferential corrosion paths |
| **Residual stresses** | Rapid quenching on impact | Cracking, delamination, reduced fatigue life |
| **Decarburization** | Carbide dissolution in flame | Loss of hardness (WC→W₂C→W) |

#### 1.1.6 Application Sectors

| Sector | Application Examples | Typical Coatings |
|--------|---------------------|-----------------|
| **Aerospace** | Turbine blades, landing gear, compressor casings | WC-Co, CrC-NiCr, MCrAlY |
| **Oil & Gas** | Valves, pump components, drill bits | WC-Co-Cr, Cr₃C₂-NiCr |
| **Automotive** | Engine cylinders, piston rings, crankshafts | Cr₃C₂-NiCr, Mo-based |
| **Marine** | Propeller shafts, rudder stocks, water turbines | Inconel 625, Hastelloy C |
| **Power Generation** | Turbine components, boiler tubes | NiCrBSi, Co-based |
| **Printing** | Anilox rolls, impression cylinders | Cr₂O₃, WC-Co |

---

### 1.2 Laser Technology for HVOF Post-Treatment

#### 1.2.1 Physics of Laser-Matter Interaction

When a laser beam strikes a coating surface, the following phenomena occur sequentially:

1. **Absorption**: Photon energy is absorbed by free electrons (metals) or lattice vibrations (ceramics). Absorption coefficient depends on wavelength, surface roughness, and material.

2. **Heat conduction**: Absorbed energy diffuses into the material following Fourier's law:
   - `∂T/∂t = κ · ∇²T` where `κ = k / (ρ · cₚ)` is thermal diffusivity

3. **Melting**: When surface temperature exceeds Tₘₑₗₜ, a melt pool forms. Melt depth depends on:
   - Laser power density (I = P / A)
   - Interaction time (t = spot diameter / scan speed)
   - Material thermal properties

4. **Resolidification**: Rapid cooling (10³–10⁶ K/s) produces refined microstructures with:
   - Reduced porosity (pores seal during melting)
   - Dissolution of oxide inclusions
   - Improved inter-splat bonding
   - Potential formation of metastable phases

#### 1.2.2 Laser Types for HVOF Remelting

| Laser Type | Wavelength | Power Range | Advantages | Disadvantages |
|-----------|-----------|-------------|------------|---------------|
| **Fiber Laser** (Yb-doped) | 1060–1080 nm | 0.1–100 kW | Best beam quality (M² ≈ 1.1), compact, low maintenance, high efficiency (30–50%), excellent for metals | Higher cost per kW |
| **CO₂ Laser** | 10.6 µm | 0.1–45 kW | Proven technology, good for ceramics (high absorption at 10.6 µm) | Large footprint, beam delivery via mirrors only, lower efficiency (10–15%) |
| **Nd:YAG Laser** | 1064 nm | 0.1–6 kW | Good fiber coupling, pulsed or CW | Lower beam quality, lamp replacement |
| **Diode Laser** | 800–980 nm | 0.1–20 kW | High electrical efficiency (50–60%), uniform top-hat profile, wide beam | Lower beam quality (M² > 10), limited focusing |

#### 1.2.3 Laser Source Selection — Recommendation

**Selected: Ytterbium Fiber Laser (CW, 1–2 kW class)**

| Parameter | Selected Value | Justification |
|-----------|---------------|---------------|
| **Type** | Ytterbium fiber laser (CW) | Best beam quality, fiber-delivered, low maintenance |
| **Power** | 1000 W (adjustable 100–1000 W) | Research on HVOF remelting shows 800–1800 W range; 1 kW is versatile |
| **Wavelength** | 1070 nm | Good absorption for metallic coatings (40–60% for Ni, Co, Fe alloys) |
| **Beam quality** | M² ≤ 1.1 | Enables tight focusing for precise treatment |
| **Spot diameter** | 0.5–3 mm (adjustable via optics) | Covers remelting and glazing regimes |
| **Beam delivery** | Single-mode fiber, 5–10 m | Flexible routing to processing chamber |
| **Cooling** | Air-cooled (up to 1 kW) | Simplifies lab setup |
| **Example model** | IPG YLR-1000 or equivalent | Industry-standard fiber laser |

**Remelting process parameters (from literature):**

| Parameter | Range | Recommended Starting Value |
|-----------|-------|---------------------------|
| **Laser power** | 400–1800 W | 800 W |
| **Scan speed** | 5–50 mm/s | 12 mm/s |
| **Spot diameter** | 1–3 mm | 2.5 mm |
| **Overlap ratio** | 30–70% | 50% |
| **Defocus distance** | 0–15 mm | 0 mm (at focus) |
| **Shielding gas** | Argon, 10–20 L/min | Argon, 15 L/min |
| **Preheat temperature** | 100–300 °C | 200 °C |

#### 1.2.4 Simulation Software Selection

**Selected: Custom Python-based digital twin simulation**

Our custom-built simulation software (already implemented) provides:
- 3D Finite Difference Method (FDM) transient heat solver
- Analytical solver for fast validation
- Gaussian and Top-Hat beam profile models
- Raster, Spiral, and Linear scan pattern generation
- AI-powered material discovery engine (genetic algorithm)
- Interactive GUI and Web-based interface

This approach was chosen over commercial software (COMSOL, ANSYS) because:
1. Full source code control and customization
2. Integration with the AI material discovery pipeline
3. Zero licensing cost
4. Educational value in understanding the underlying physics

---

### 1.3 Alloy Systems for HVOF Coatings

#### 1.3.1 Common HVOF Coating Alloy Families

| Alloy Family | Typical Composition | Tₘₑₗₜ (°C) | Hardness (HV) | Primary Use |
|-------------|--------------------|----|-------|-------------|
| **WC-Co** | WC-12%Co, WC-17%Co | 1495 (Co binder) | 1000–1400 | Wear resistance, mining, oil & gas |
| **WC-Co-Cr** | WC-10Co-4Cr | 1495 | 1100–1500 | Wear + corrosion resistance |
| **Cr₃C₂-NiCr** | 75Cr₃C₂-25NiCr | 1350 | 800–1100 | High-temperature wear (up to 900°C) |
| **NiCrBSi** | Ni-15Cr-3.5B-4.5Si | 1020–1060 | 700–900 | Self-fluxing, corrosion resistance |
| **MCrAlY** (M = Ni, Co) | Ni-22Cr-10Al-1Y | 1300–1400 | 300–500 | Thermal barrier bond coats |
| **Stellite (Co-Cr-W)** | Co-28Cr-4W-1C | 1260–1355 | 400–600 | High-temperature wear, cavitation |
| **Inconel 625** | Ni-22Cr-9Mo-3.5Nb | 1290–1350 | 200–350 | Corrosion resistance, marine |
| **Hastelloy C-276** | Ni-16Cr-16Mo-4W | 1325–1370 | 200–300 | Severe corrosion environments |

#### 1.3.2 Key Alloy Elements — Properties and Roles

| Element | Symbol | Tₘₑₗₜ (°C) | Density (kg/m³) | Role in Coatings |
|---------|--------|------|--------|-----------------|
| **Aluminium** | Al | 660 | 2700 | Oxidation resistance (forms Al₂O₃ scale), lightweight, MCrAlY bond coats |
| **Nickel** | Ni | 1455 | 8908 | Matrix element, corrosion resistance, ductility, high-temperature stability |
| **Cobalt** | Co | 1495 | 8900 | Binder in WC-Co, high-temp strength, wear resistance |
| **Chromium** | Cr | 1907 | 7190 | Corrosion resistance (passive Cr₂O₃ layer), hardness, carbide formation |
| **Tungsten** | W | 3422 | 19250 | Highest Tₘₑₗₜ of any metal, extreme hardness as WC, wear resistance |
| **Molybdenum** | Mo | 2623 | 10280 | High-temperature strength, corrosion resistance in reducing environments |
| **Titanium** | Ti | 1668 | 4506 | Lightweight, biocompatibility, TiC/TiN hard phases |
| **Tantalum** | Ta | 3017 | 16690 | Ultra-high-temperature applications, corrosion resistance |
| **Hafnium** | Hf | 2233 | 13310 | Ta₄HfC₅ has highest known Tₘₑₗₜ (4215°C) |
| **Carbon** | C | 3550 | 2267 | Forms carbides (WC, Cr₃C₂, TiC), extreme hardness |
| **Niobium** | Nb | 2477 | 8570 | Refractory applications, superconductors |
| **Vanadium** | V | 1910 | 6110 | Carbide stabilizer, strengthening |
| **Zirconium** | Zr | 1855 | 6520 | Thermal barrier coatings (YSZ), nuclear applications |
| **Iron** | Fe | 1538 | 7874 | Base for stainless steels, low cost |
| **Yttrium** | Y | 1526 | 4472 | Oxide scale adhesion in MCrAlY, YSZ stabilizer |

#### 1.3.3 Effect of Laser Treatment on Different Alloy Systems

| Alloy System | Before Laser Treatment | After Laser Treatment | Key Improvement |
|-------------|----------------------|---------------------|-----------------|
| **WC-Co** | Porosity 1–3%, some WC→W₂C decarburization | Dense remelted layer, risk of further WC dissolution | Must control power to avoid WC dissolution |
| **Cr₃C₂-NiCr** | Oxide inclusions, lamellar structure | Homogenized matrix, refined carbides | Improved high-temp oxidation resistance |
| **NiCrBSi** | Semi-porous, splat boundaries | Fully dense, metallurgically bonded | Significantly improved corrosion resistance |
| **Stellite** | Partially melted Co matrix | Homogeneous dendritic structure | Enhanced wear and cavitation resistance |
| **MCrAlY** | Oxide stringers between splats | Clean, oxide-free bond coat | Better thermal barrier system performance |

---

## PART 2: Laboratory Design — X-Y Table & Experimental Setup

---

### 2.1 Motorized X-Y Table Design

#### 2.1.1 System Overview

The motorized X-Y table is the core positioning system that moves HVOF-coated samples beneath the stationary laser beam with high precision and repeatability. The system must provide:
- Precise positioning for uniform laser coverage
- Programmable scan patterns (raster, spiral, linear)
- Controlled speed for consistent energy input
- Sufficient travel range for practical sample sizes

#### 2.1.2 Component Selection

##### A. Linear Stages (X and Y axes)

| Specification | Selected Value | Justification |
|--------------|---------------|---------------|
| **Model** | PI (Physik Instrumente) M-531.DD or Newport IMS-300-LM | Industry-standard precision stages |
| **Travel range** | 300 × 300 mm | Covers most HVOF sample sizes |
| **Resolution** | ≤ 1 µm | Sub-spot-diameter positioning accuracy |
| **Repeatability** | ± 0.5 µm | Ensures consistent overlap between passes |
| **Maximum speed** | 250 mm/s | Exceeds typical scan speed (5–50 mm/s) |
| **Load capacity** | 50 kg (horizontal) | Supports chamber + sample + fixtures |
| **Drive mechanism** | Ball screw (preloaded, zero-backlash) | Balance of precision, speed, and cost |
| **Bearing type** | Crossed-roller or recirculating ball | Smooth motion, high stiffness |
| **Feedback** | Linear encoder (0.1 µm resolution) | Closed-loop position verification |
| **Material** | Anodized aluminium body, steel guideways | Lightweight, thermally stable |

##### B. Motor Selection

| Specification | Selected Value | Justification |
|--------------|---------------|---------------|
| **Motor type** | Stepper motor with micro-stepping | Cost-effective, sufficient for scan speeds ≤ 250 mm/s |
| **Alternative** | Servo motor (brushless DC) | Higher performance if budget allows |
| **Micro-step resolution** | 1/256 | Smooth motion, reduces vibration |
| **Holding torque** | ≥ 1 N·m | Prevents drift during processing |
| **Encoder** | Incremental (10,000 counts/rev) | Closed-loop position feedback |

##### C. Motion Controller

| Specification | Selected Value | Justification |
|--------------|---------------|---------------|
| **Controller** | PI C-884.4DC or Newport XPS-Q8 | Multi-axis, programmable, industrial-grade |
| **Alternative (budget)** | Galil DMC-4183 or Arduino + GRBL shield | Lower cost for educational lab |
| **Axes** | 2 (X, Y) with expansion for Z if needed | Minimum for planar scanning |
| **Interface** | USB / Ethernet / RS-232 | PC connectivity |
| **Programming** | Python API / G-code compatible | Integration with existing simulation software |
| **Trajectory generation** | Linear + circular interpolation | Supports raster and spiral patterns |
| **Synchronization** | Trigger output for laser ON/OFF | Laser fires only during scanning motion |

##### D. Control Software

| Requirement | Implementation |
|------------|---------------|
| **Scan pattern definition** | Already implemented in our simulation: `raster_generator.py`, `spiral_generator.py`, `linear_generator.py` |
| **G-code generation** | Convert trajectory (x, y, t) arrays to G-code commands |
| **Real-time monitoring** | Position display, speed readout, progress tracking |
| **Parameter storage** | Save/load scan recipes (speed, overlap, pattern type) |
| **Safety interlocks** | Software enforces travel limits, emergency stop |
| **Interface** | Python GUI (extension of existing `interactive_gui.py`) or dedicated LabVIEW/Python control panel |

#### 2.1.3 X-Y Table Assembly Drawing

```
    ┌─────────────────────────────────────────────────────┐
    │                 LASER BEAM (from above)              │
    │                        ↓                             │
    │              ┌─────────────────┐                     │
    │              │  Focusing Optics │                    │
    │              │  (f = 200 mm)    │                    │
    │              └────────┬────────┘                     │
    │                       │                              │
    │  ┌────────────────────┼────────────────────┐         │
    │  │    ALUMINIUM PROCESSING CHAMBER         │         │
    │  │  ┌──────────────────────────────────┐   │         │
    │  │  │         QUARTZ WINDOW            │   │         │
    │  │  └──────────────────────────────────┘   │         │
    │  │                                         │         │
    │  │  ┌──────────── SAMPLE ──────────────┐   │ ← Ar   │
    │  │  │  (HVOF-coated substrate)         │   │  gas in │
    │  │  └──────────── ───────── ───────────┘   │         │
    │  │         ↕ mounted on fixture            │         │
    │  │  ┌──────────────────────────────────┐   │         │
    │  │  │         HEATER PLATE             │   │         │
    │  │  │       (preheat to 200°C)         │   │         │
    │  │  └──────────────────────────────────┘   │         │
    │  └─────────────────────────────────────────┘         │
    │                       │                              │
    │  ╔════════════════════╧════════════════════╗         │
    │  ║          Y-AXIS LINEAR STAGE            ║         │
    │  ║  ← ─ ─ ─ ─ ─ 300 mm travel ─ ─ ─ ─ → ║         │
    │  ╚════════════════════╤════════════════════╝         │
    │                       │                              │
    │  ╔════════════════════╧════════════════════╗         │
    │  ║          X-AXIS LINEAR STAGE            ║         │
    │  ║  ← ─ ─ ─ ─ ─ 300 mm travel ─ ─ ─ ─ → ║         │
    │  ╚════════════════════╤════════════════════╝         │
    │                       │                              │
    │  ┌────────────────────┴────────────────────┐         │
    │  │        RIGID STEEL OPTICAL TABLE         │        │
    │  │       (vibration-damped, M6 holes)       │        │
    │  └─────────────────────────────────────────┘         │
    └─────────────────────────────────────────────────────┘
```

---

### 2.2 Steel Framework & Laser Mounting

#### 2.2.1 Optical Table

| Specification | Value |
|--------------|-------|
| **Type** | Research-grade optical table (breadboard) |
| **Model** | Thorlabs PBG52507 or Newport RS-2000 |
| **Size** | 1200 × 900 × 60 mm |
| **Material** | Stainless steel top plate, honeycomb core |
| **Mounting holes** | M6 on 25 mm grid |
| **Flatness** | ± 0.1 mm over entire surface |
| **Vibration isolation** | Passive pneumatic legs or elastomeric feet |

#### 2.2.2 Laser Head Mounting (Gantry/Post System)

| Component | Specification |
|-----------|--------------|
| **Vertical post** | Stainless steel, 500 mm height, Ø 50 mm |
| **Post holder** | Clamping base, M6 bolt to optical table |
| **Laser head mount** | Custom bracket, adjustable height (Z-axis manual) |
| **Focusing optics holder** | SM1 tube system (Thorlabs) or equivalent |
| **Adjustment** | Fine Z-height adjustment (± 25 mm) for defocus control |

---

### 2.3 Fibre Optic Beam Delivery

| Component | Specification |
|-----------|--------------|
| **Fibre type** | Single-mode, armoured process fibre |
| **Core diameter** | 50–100 µm (for 1 kW CW fiber laser) |
| **Length** | 5–10 m (laser source to processing head) |
| **Connector** | QBH or LLK-D (industry standard) |
| **Collimator** | f = 100 mm collimation lens module |
| **Focusing lens** | f = 200 mm (gives ~2× magnification, spot ~100–200 µm) |
| **Protective window** | Fused silica, AR-coated at 1070 nm, replaceable |
| **Gas shield nozzle** | Coaxial argon delivery around beam exit |

---

### 2.4 Aluminium Processing Chamber

| Specification | Value |
|--------------|-------|
| **Material** | 6061-T6 Aluminium alloy |
| **Internal dimensions** | 400 × 400 × 200 mm |
| **Wall thickness** | 10 mm |
| **Top window** | Fused silica, 100 mm diameter, AR-coated at 1070 nm |
| **Gas inlet** | 2× KF-25 flanges for argon supply |
| **Gas outlet** | 1× KF-25 flange with valve for purging |
| **Sample access** | Hinged front door with safety interlock switch |
| **Sample fixture** | T-slot base plate with adjustable clamps |
| **Sensor ports** | 2× thermocouple feedthroughs, 1× pyrometer window |
| **Sealing** | Viton O-ring gaskets on all joints |

---

### 2.5 Controlled Atmosphere System

| Component | Specification |
|-----------|--------------|
| **Shielding gas** | High-purity Argon (99.999%) |
| **Gas supply** | 50 L cylinder at 200 bar, or building supply |
| **Regulator** | Two-stage regulator, 0–30 L/min |
| **Flow controller** | Mass flow controller (MFC), 0–20 L/min range |
| **Model** | Bronkhorst EL-FLOW or MKS 1179C |
| **Purpose** | Prevent oxidation during laser remelting |
| **Chamber purge** | 3× volume exchanges before processing |
| **Oxygen monitor** | Inline O₂ sensor, alarm at > 100 ppm |
| **Exhaust** | Fume extraction via HEPA + activated carbon filter |

---

### 2.6 Electrical & Electronic Components

| Component | Specification | Qty |
|-----------|--------------|-----|
| **Motion controller** | PI C-884 or Newport XPS-Q8 | 1 |
| **Stepper motor driver** | Integrated in controller | 2 |
| **Power supply (motors)** | 48V DC, 10A | 1 |
| **Laser power supply** | Integrated with fiber laser unit | 1 |
| **Emergency stop (E-stop)** | Mushroom-head, latching, dual-channel | 2 |
| **Safety relay module** | Pilz PNOZ or equivalent, SIL 3 | 1 |
| **Door interlock switch** | Magnetic safety switch (Schmersal) | 2 |
| **Status indicator lights** | Green (safe) / Amber (standby) / Red (laser active) | 1 set |
| **Warning beacon** | Rotating amber/red beacon at lab entrance | 1 |
| **PLC (optional)** | Siemens LOGO! or Allen-Bradley Micro820 | 1 |
| **Temperature controller** | PID controller for heater plate (Eurotherm) | 1 |
| **Thermocouple** | K-type, Ø 1 mm, for sample temperature monitoring | 4 |
| **Pyrometer (optional)** | Non-contact IR pyrometer for melt pool monitoring | 1 |
| **Control PC** | Desktop PC with Python, dedicated to lab control | 1 |

---

### 2.7 Safety Measures

#### 2.7.1 Laser Safety Classification

The 1 kW fiber laser is **Class 4** (highest hazard class per IEC 60825-1). Requirements:

| Safety Measure | Implementation |
|---------------|---------------|
| **Laser safety officer (LSO)** | Designated trained person |
| **Laser safety eyewear** | OD 7+ at 1070 nm for all personnel |
| **Controlled access** | Magnetic lock on lab door, key-card access |
| **Door interlock** | Automatically closes laser shutter if door opens |
| **Warning signs** | "DANGER: Class 4 Laser" at all entry points |
| **Warning light** | Illuminated sign "LASER IN USE" when active |
| **Beam enclosure** | Fully enclosed beam path from fibre exit to chamber window |
| **Emergency stop** | Red E-stop buttons at door AND at workstation |
| **Stray beam control** | Diffuse-reflective matte black surfaces on all nearby equipment |
| **Fire prevention** | No flammable materials within 2 m, fire extinguisher in lab |
| **Training** | All users must complete laser safety training before access |

#### 2.7.2 Ventilation & Extraction

| Item | Specification |
|------|--------------|
| **Local exhaust** | Extraction hood over chamber exhaust port |
| **Extraction rate** | ≥ 500 m³/h |
| **Filter system** | HEPA H14 + activated carbon for fume removal |
| **Exhaust fan** | Centrifugal blower, located at building exterior |
| **Ducting** | Galvanized steel, operated under negative pressure |
| **Makeup air** | Forced-air inlet from opposite side of lab |
| **CO/O₂ monitor** | Wall-mounted gas detector with audible alarm |

#### 2.7.3 General Lab Safety

| Item | Details |
|------|--------|
| **PPE** | Lab coat, safety glasses, laser eyewear (when door open), hearing protection (during HVOF) |
| **Emergency procedures** | Evacuation plan posted, first-aid kit available |
| **Electrical safety** | RCD-protected power outlets, proper grounding |
| **Material handling** | SDS sheets for all powders, dust collection for powder handling |

---

### 2.8 Complete Laboratory Layout

```
    ┌───────────────────────────────────────────────────────────────────┐
    │                     LASER TREATMENT LABORATORY                    │
    │                        (approx. 6 m × 5 m)                       │
    │                                                                   │
    │   ┌──────────┐                                                    │
    │   │  LASER   │ ◄── Fiber laser source (air-cooled)               │
    │   │  SOURCE  │     with integrated power supply                   │
    │   │  UNIT    │                                                    │
    │   └────┬─────┘                                                    │
    │        │ fiber optic cable (5–10 m)                               │
    │        │                                                          │
    │   ┌────┴────────────────────────────────────────┐                 │
    │   │              PROCESSING AREA                 │                │
    │   │  ┌─────────────────────────────────────┐     │                │
    │   │  │       OPTICAL TABLE (1200×900)       │     │                │
    │   │  │  ┌──────────┐  ┌────────────┐       │     │                │
    │   │  │  │  LASER   │  │ ALUMINIUM  │       │     │                │
    │   │  │  │  HEAD +  │  │  CHAMBER   │       │     │                │
    │   │  │  │  OPTICS  │  │  + SAMPLE  │       │     │                │
    │   │  │  └──────────┘  └────────────┘       │     │                │
    │   │  │       ┌──────────────────┐          │     │                │
    │   │  │       │  X-Y STAGES     │          │     │                │
    │   │  │       └──────────────────┘          │     │                │
    │   │  └─────────────────────────────────────┘     │                │
    │   │                                              │                │
    │   │  [E-STOP]                     [E-STOP]       │                │
    │   └──────────────────────────────────────────────┘                │
    │                                                                   │
    │   ┌───────────┐  ┌───────────┐  ┌────────────────┐               │
    │   │  CONTROL  │  │    GAS    │  │   EXTRACTION   │               │
    │   │    PC +   │  │  SYSTEM   │  │    + FILTER    │               │
    │   │ CONTROLLER│  │ (Argon)   │  │    SYSTEM      │               │
    │   └───────────┘  └───────────┘  └────────────────┘               │
    │                                                                   │
    │  🔴 [LASER IN USE] sign                                          │
    │  ⚠️ DANGER: CLASS 4 LASER                                        │
    │  🚪 DOOR WITH MAGNETIC INTERLOCK                                 │
    └───────────────────────────────────────────────────────────────────┘
```

---

## PART 3: Economic Assessment

### 3.1 Equipment Cost Breakdown

| Category | Item | Estimated Cost (€) |
|----------|------|-------------------|
| **Laser System** | 1 kW Ytterbium fiber laser (IPG YLR-1000 or equivalent) | 40,000–80,000 |
| | Processing head with collimator + focus lens | 3,000–5,000 |
| | Armoured process fibre (10 m) | 2,000–3,000 |
| | Protective windows (5× spare) | 500 |
| **X-Y Table** | 2× Linear stages 300 mm travel | 8,000–15,000 |
| | Stepper motors + encoders (2×) | 1,500–3,000 |
| | Motion controller (2-axis) | 3,000–6,000 |
| **Optical table** | 1200×900 mm breadboard + isolation legs | 3,000–5,000 |
| **Mounting hardware** | Posts, holders, brackets | 1,000–2,000 |
| **Processing chamber** | Custom aluminium chamber with windows | 3,000–5,000 |
| **Gas system** | MFC + regulator + fittings + Ar cylinder | 2,000–4,000 |
| **Safety** | Interlocks, E-stops, warning lights, safety relay | 2,000–3,000 |
| | Laser safety eyewear (5 pairs) | 1,000–2,000 |
| | Signage + warning beacon | 300–500 |
| **Extraction** | Fume extraction + HEPA filter system | 3,000–5,000 |
| **Electrical** | Power distribution, RCD, wiring | 1,500–2,500 |
| **Control PC** | Desktop PC + monitors + software | 1,500–2,000 |
| **Temperature monitoring** | PID controller + thermocouples + pyrometer | 2,000–4,000 |
| | | |
| | **SUBTOTAL (Equipment)** | **~76,300–147,000** |

### 3.2 Installation & Setup Costs

| Item | Estimated Cost (€) |
|------|-------------------|
| Lab space preparation (electrical, ventilation modifications) | 5,000–10,000 |
| Equipment installation and alignment | 3,000–5,000 |
| Safety certification and inspection | 2,000–4,000 |
| Training for lab personnel | 1,000–2,000 |
| **SUBTOTAL (Installation)** | **~11,000–21,000** |

### 3.3 Annual Operational Costs

| Item | Estimated Cost (€/year) |
|------|------------------------|
| Argon gas supply (50 L cylinders, ~12/year) | 3,000–5,000 |
| Electricity (laser + equipment) | 2,000–4,000 |
| Consumables (protective windows, filters, nozzles) | 1,000–2,000 |
| Maintenance and calibration | 2,000–4,000 |
| Laser safety officer time (partial FTE) | 3,000–5,000 |
| **SUBTOTAL (Annual Operations)** | **~11,000–20,000** |

### 3.4 Total Project Budget Summary

| Category | Low Estimate (€) | High Estimate (€) |
|----------|------------------|-------------------|
| Equipment | 76,300 | 147,000 |
| Installation | 11,000 | 21,000 |
| **Total Capital** | **87,300** | **168,000** |
| Annual Operations | 11,000 | 20,000 |
| **5-Year Total Cost** | **142,300** | **268,000** |

> **Note**: Costs are approximate and based on 2024/2025 European market prices. Actual costs will vary by supplier, quantities, and institutional discounts. The HVOF system cost (~€100K–500K) is NOT included as the project assumes access to existing HVOF equipment.

---

## References

1. ASM International, *ASM Handbook Vol. 5A: Thermal Spray Technology*, ASM International, 2013.
2. J.R. Davis (Ed.), *Handbook of Thermal Spray Technology*, ASM International, 2004.
3. L. Pawlowski, *The Science and Engineering of Thermal Spray Coatings*, 2nd ed., John Wiley & Sons, 2008.
4. P.L. Fauchais, J.V.R. Heberlein, M.I. Boulos, *Thermal Spray Fundamentals*, Springer, 2014.
5. IEC 60825-1:2014, *Safety of laser products*, International Electrotechnical Commission.
6. ANSI Z136.1-2014, *Safe Use of Lasers*, American National Standards Institute.
7. Materials Project Database, https://materialsproject.org/
8. Thorlabs Inc., Motion Control Product Catalog, https://www.thorlabs.com/
9. PI (Physik Instrumente), Precision Positioning Stages, https://www.pi-usa.us/
10. Newport/MKS Instruments, Motion Control Systems, https://www.newport.com/
11. IPG Photonics, Fiber Laser Product Line, https://www.ipgphotonics.com/
