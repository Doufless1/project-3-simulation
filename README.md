# HVOF Laser Lab Simulation & Control

**Virtual Digital Twin for High-Velocity Oxy-Fuel (HVOF) Laser Surface Treatment.**

![Dashboard](https://via.placeholder.com/800x400?text=HVOF+Laser+Lab+Dashboard)

## 🚀 Overview

This project is a high-fidelity **Digital Twin** simulation of a laser surface treatment laboratory. It provides a professional, Clean Architecture-based control system for managing a virtual X-Y table, high-power fiber laser, and shielding gas system.

Key capabilities include:
-   **Real-time Simulation**: Virtual hardware responds with realistic delays and physics-based logic (e.g., thermal dynamics, gas flow).
-   **Professional Dashboard**: A Dash-based UI with "NanoBanana" styling, 3D visualization, and animated feedback.
-   **Robust Safety**: Implements **STRIDE** security principles (Audit Logs, Repudiation Defense) and critical interlocks.
-   **Cost Management**: Integrated equipment catalog and budget calculator.

## 🛠️ Architecture

The system follows **Clean Architecture** principles to ensure maintainability and testability:

1.  **Domain Layer**: Pure Python entities representing the core lab hardware (Laser, Table, Gas) and logic. Zero external dependencies.
2.  **Infrastructure Layer**: Adapters for "Virtual Hardware" that simulate real-world behavior and audit logging.
3.  **Application Layer**: Use cases that orchestrate domain logic (e.g., `RunScan`, `EmergencyStop`).
4.  **Presentation Layer**: The Plotly Dash web application that interacts with the user.

## 📦 Installation & Running

### Prerequisites
-   Python 3.8+
-   `pip`

### Setup
1.  **Clone the repository**:
    ```bash
    git clone <repo-url>
    cd hvof-lab-control
    ```

2.  **Install dependencies**:
    ```bash
    pip install dash pandas plotly numpy pytest
    ```

3.  **Generate Icons** (First time only):
    ```bash
    python tools/generate_icons.py
    # Copy generated icons to the app assets
    # Windows (PowerShell):
    Copy-Item -Path "assets\icons\*.svg" -Destination "lab_control\presentation\assets\icons\"
    ```

### Launching the Dashboard
Run the entry point script:
```bash
python lab_control/run_dashboard.py
```
Open your browser to: **http://127.0.0.1:8051**

## 🖥️ User Guide

### 1. X-Y Table & Scanning
-   **Position Control**: Home the table before running scans.
-   **Scan Parameters**: Configure the G-code generation (Pattern, Speed, Power).
-   **Visualization**: Watch the real-time 3D path plotting on the right.

### 2. Laser Control
-   **Power**: Set the laser power (0-1000W).
-   **Arm/Fire**: Safety sequence is enforced (Arm -> Fire).
-   **Status**: Monitor "NanoBanana" styled status indicators.

### 3. Safety System
-   **Interlocks**: Lock the Door and Chamber before operation.
-   **E-STOP**: Immediately halts all operations. Reset required to resume.
-   **Audit Log**: Tracks all critical actions for security review.

### 4. 3D Laboratory
-   Explore the realistic **"NanoBanana" 3D Lab** layout.
-   Drag to rotate, scroll to zoom.
-   Visualizes key components: Laser Head, Table, Gas Tanks, and Safety Zones.

### 5. Cost Calculator
-   Analyze equipment budget by category.
-   Clean "Pie Chart" and "Briefcase" visualizations.

## 🛡️ Security (STRIDE)
-   **Spoofing**: Simulated secure sessions.
-   **Tampering**: Input validation on all setpoints.
-   **Repudiation**: Immutable Audit Logs.
-   **Information Disclosure**: Error masking in UI.
-   **Denial of Service**: Rate limiting simulation.
-   **Elevation of Privilege**: Role-based access simulation.

---
