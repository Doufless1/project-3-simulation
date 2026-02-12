"""
Interactive 3D Simulation GUI — Laser-HVOF Treatment Visualizer.

Features:
- Interactive 3D temperature field you can rotate/zoom/pan
- Material selector (ULTRA-C-Ta-WH-3849, SS316L, WC-12Co)
- Laser parameter sliders (power, spot radius)
- Motion controls (speed, pattern type)
- Real-time re-simulation with "Run Simulation" button
- Multiple visualization tabs (3D Surface, XZ/YZ Cross-sections, Depth)
- Results panel showing peak temperature, fluence, melt depth

Usage:
    python -m presentation.interactive_gui
"""

import sys
import os
import tkinter as tk
from tkinter import ttk, messagebox
import threading
import numpy as np

# Ensure project root is in path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

import matplotlib
matplotlib.use("TkAgg")
import matplotlib.pyplot as plt
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg, NavigationToolbar2Tk
from matplotlib.figure import Figure
from mpl_toolkits.mplot3d import Axes3D
from matplotlib import cm

# Domain
from domain.entities import Material, LaserBeam, SimulationResult

# Infrastructure
from infrastructure.laser.gaussian_source import GaussianLaserSource
from infrastructure.laser.tophat_source import TopHatLaserSource
from infrastructure.laser.laser_factory import LaserFactory
from infrastructure.motion.raster_generator import RasterGenerator
from infrastructure.motion.spiral_generator import SpiralGenerator
from infrastructure.motion.linear_generator import LinearGenerator
from infrastructure.solvers.fdm_heat_solver_3d import FDMHeatSolver3D
from infrastructure.solvers.analytical_solver import AnalyticalHeatSolver
from infrastructure.logging.audit_logger import AuditLogger

# Application
from application.simulation_use_case import SimulationUseCase


# ============================================================================
# Material Presets (including user's ULTRA-C-Ta-WH-3849)
# ============================================================================

MATERIAL_PRESETS = {
    "ULTRA-C-Ta-WH-3849": Material(
        name="ULTRA-C-Ta-WH-3849",
        absorption=0.70,
        thermal_conductivity=60.0,
        density=9939.0,
        specific_heat=386.0,
        t_ambient=20.0,
        t_melt=3376.0,
        t_vaporization=4800.0,
    ),
    "316L Stainless Steel": Material(
        name="316L Stainless Steel",
        absorption=0.35,
        thermal_conductivity=15.0,
        density=8000.0,
        specific_heat=500.0,
        t_ambient=20.0,
        t_melt=1400.0,
        t_vaporization=2800.0,
    ),
    "WC-12Co (HVOF Coating)": Material(
        name="WC-12Co (HVOF Coating)",
        absorption=0.55,
        thermal_conductivity=80.0,
        density=14500.0,
        specific_heat=240.0,
        t_ambient=20.0,
        t_melt=2870.0,
        t_vaporization=6000.0,
    ),
}

BEAM_PROFILES = {"Gaussian (TEM00)": "gaussian", "Top-Hat (Flat)": "tophat"}
MOTION_PATTERNS = {"Raster (Zig-Zag)": "raster", "Spiral": "spiral", "Linear": "linear"}
SOLVER_TYPES = {"3D FDM (Full)": "fdm3d", "Analytical (Fast)": "analytical"}


# ============================================================================
# Color Palette
# ============================================================================

BG_DARK = "#1a1a2e"
BG_PANEL = "#16213e"
BG_INPUT = "#0f3460"
FG_TEXT = "#e0e0e0"
FG_ACCENT = "#00d4ff"
FG_WARN = "#ff6b6b"
FG_SUCCESS = "#51cf66"
BTN_RUN = "#e94560"
BTN_RUN_HOVER = "#ff2e63"


# ============================================================================
# Main GUI Application
# ============================================================================

class SimulationGUI:
    """Interactive 3D simulation GUI with parameter controls."""

    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("🔬 Laser-HVOF 3D Simulation — Interactive Lab")
        self.root.geometry("1400x900")
        self.root.configure(bg=BG_DARK)
        self.root.minsize(1200, 750)

        # State
        self._result = None
        self._running = False

        # Configure ttk styles
        self._setup_styles()

        # Build UI
        self._build_ui()

    # ================================================================
    # Styles
    # ================================================================

    def _setup_styles(self):
        style = ttk.Style()
        style.theme_use("clam")

        style.configure("Dark.TFrame", background=BG_DARK)
        style.configure("Panel.TFrame", background=BG_PANEL)
        style.configure(
            "Dark.TLabel", background=BG_PANEL, foreground=FG_TEXT,
            font=("Segoe UI", 10),
        )
        style.configure(
            "Header.TLabel", background=BG_PANEL, foreground=FG_ACCENT,
            font=("Segoe UI", 11, "bold"),
        )
        style.configure(
            "Result.TLabel", background=BG_PANEL, foreground=FG_SUCCESS,
            font=("Consolas", 11),
        )
        style.configure(
            "Title.TLabel", background=BG_DARK, foreground=FG_ACCENT,
            font=("Segoe UI", 16, "bold"),
        )
        style.configure(
            "TCombobox", fieldbackground=BG_INPUT, foreground=FG_TEXT,
            background=BG_INPUT,
        )
        style.configure(
            "Dark.TNotebook", background=BG_DARK,
        )
        style.configure(
            "Dark.TNotebook.Tab", background=BG_PANEL, foreground=FG_TEXT,
            padding=[12, 4], font=("Segoe UI", 10),
        )
        style.map(
            "Dark.TNotebook.Tab",
            background=[("selected", BG_INPUT)],
            foreground=[("selected", FG_ACCENT)],
        )

    # ================================================================
    # UI Construction
    # ================================================================

    def _build_ui(self):
        # Title bar
        title_frame = ttk.Frame(self.root, style="Dark.TFrame")
        title_frame.pack(fill="x", padx=10, pady=(10, 5))
        ttk.Label(
            title_frame, text="🔬 Laser-HVOF 3D Simulation Lab",
            style="Title.TLabel",
        ).pack(side="left")

        # Main container: left panel + right canvas
        main_frame = ttk.Frame(self.root, style="Dark.TFrame")
        main_frame.pack(fill="both", expand=True, padx=10, pady=5)

        # LEFT: Control panel
        self._build_control_panel(main_frame)

        # RIGHT: Visualization area
        self._build_visualization_area(main_frame)

    def _build_control_panel(self, parent):
        panel = ttk.Frame(parent, style="Panel.TFrame", width=320)
        panel.pack(side="left", fill="y", padx=(0, 10), pady=0)
        panel.pack_propagate(False)

        canvas = tk.Canvas(panel, bg=BG_PANEL, highlightthickness=0)
        scrollbar = ttk.Scrollbar(panel, orient="vertical", command=canvas.yview)
        scroll_frame = ttk.Frame(canvas, style="Panel.TFrame")

        scroll_frame.bind(
            "<Configure>",
            lambda e: canvas.configure(scrollregion=canvas.bbox("all")),
        )
        canvas.create_window((0, 0), window=scroll_frame, anchor="nw", width=300)
        canvas.configure(yscrollcommand=scrollbar.set)

        canvas.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")

        # Bind mouse wheel
        def _on_mousewheel(event):
            canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")
        canvas.bind_all("<MouseWheel>", _on_mousewheel)

        inner = scroll_frame
        pad = {"padx": 10, "pady": 3}

        # --- Material Section ---
        self._section_header(inner, "⚗️ MATERIAL")
        self.material_var = tk.StringVar(value="ULTRA-C-Ta-WH-3849")
        mat_combo = ttk.Combobox(
            inner, textvariable=self.material_var,
            values=list(MATERIAL_PRESETS.keys()), state="readonly", width=30,
        )
        mat_combo.pack(**pad)
        mat_combo.bind("<<ComboboxSelected>>", self._on_material_change)

        # Material info label
        self.mat_info_label = ttk.Label(inner, text="", style="Dark.TLabel",
                                         wraplength=280)
        self.mat_info_label.pack(**pad)
        self._update_material_info()

        self._separator(inner)

        # --- Laser Section ---
        self._section_header(inner, "🔴 LASER")

        ttk.Label(inner, text="Power [W]:", style="Dark.TLabel").pack(**pad)
        self.power_var = tk.DoubleVar(value=500.0)
        self.power_slider = tk.Scale(
            inner, from_=50, to=5000, orient="horizontal",
            variable=self.power_var, resolution=50,
            bg=BG_PANEL, fg=FG_TEXT, troughcolor=BG_INPUT,
            highlightthickness=0, length=280,
        )
        self.power_slider.pack(**pad)

        ttk.Label(inner, text="Spot Radius [µm]:", style="Dark.TLabel").pack(**pad)
        self.spot_var = tk.DoubleVar(value=50.0)
        self.spot_slider = tk.Scale(
            inner, from_=10, to=500, orient="horizontal",
            variable=self.spot_var, resolution=5,
            bg=BG_PANEL, fg=FG_TEXT, troughcolor=BG_INPUT,
            highlightthickness=0, length=280,
        )
        self.spot_slider.pack(**pad)

        ttk.Label(inner, text="Beam Profile:", style="Dark.TLabel").pack(**pad)
        self.beam_var = tk.StringVar(value="Gaussian (TEM00)")
        ttk.Combobox(
            inner, textvariable=self.beam_var,
            values=list(BEAM_PROFILES.keys()), state="readonly", width=30,
        ).pack(**pad)

        self._separator(inner)

        # --- Motion Section ---
        self._section_header(inner, "🔄 MOTION")

        ttk.Label(inner, text="Scan Speed [mm/s]:", style="Dark.TLabel").pack(**pad)
        self.speed_var = tk.DoubleVar(value=100.0)
        self.speed_slider = tk.Scale(
            inner, from_=10, to=1000, orient="horizontal",
            variable=self.speed_var, resolution=10,
            bg=BG_PANEL, fg=FG_TEXT, troughcolor=BG_INPUT,
            highlightthickness=0, length=280,
        )
        self.speed_slider.pack(**pad)

        ttk.Label(inner, text="Pattern:", style="Dark.TLabel").pack(**pad)
        self.motion_var = tk.StringVar(value="Raster (Zig-Zag)")
        ttk.Combobox(
            inner, textvariable=self.motion_var,
            values=list(MOTION_PATTERNS.keys()), state="readonly", width=30,
        ).pack(**pad)

        self._separator(inner)

        # --- Solver Section ---
        self._section_header(inner, "⚙️ SOLVER")

        ttk.Label(inner, text="Solver Type:", style="Dark.TLabel").pack(**pad)
        self.solver_var = tk.StringVar(value="3D FDM (Full)")
        ttk.Combobox(
            inner, textvariable=self.solver_var,
            values=list(SOLVER_TYPES.keys()), state="readonly", width=30,
        ).pack(**pad)

        ttk.Label(inner, text="Resolution [µm]:", style="Dark.TLabel").pack(**pad)
        self.res_var = tk.DoubleVar(value=200.0)
        self.res_slider = tk.Scale(
            inner, from_=50, to=500, orient="horizontal",
            variable=self.res_var, resolution=25,
            bg=BG_PANEL, fg=FG_TEXT, troughcolor=BG_INPUT,
            highlightthickness=0, length=280,
        )
        self.res_slider.pack(**pad)

        self._separator(inner)

        # --- Run Button ---
        self.run_btn = tk.Button(
            inner, text="▶  RUN SIMULATION",
            font=("Segoe UI", 13, "bold"),
            bg=BTN_RUN, fg="white", activebackground=BTN_RUN_HOVER,
            activeforeground="white", relief="flat", cursor="hand2",
            command=self._run_simulation, height=2,
        )
        self.run_btn.pack(padx=10, pady=15, fill="x")

        # --- Status ---
        self.status_label = ttk.Label(
            inner, text="Ready. Configure parameters and press RUN.",
            style="Dark.TLabel", wraplength=280,
        )
        self.status_label.pack(**pad)

        self._separator(inner)

        # --- Results Section ---
        self._section_header(inner, "📊 RESULTS")

        self.results_text = tk.Text(
            inner, height=12, bg=BG_INPUT, fg=FG_SUCCESS,
            font=("Consolas", 10), relief="flat", wrap="word",
            insertbackground=FG_TEXT,
        )
        self.results_text.pack(padx=10, pady=5, fill="x")
        self.results_text.insert("1.0", "No results yet.\nRun a simulation first.")
        self.results_text.config(state="disabled")

    def _build_visualization_area(self, parent):
        viz_frame = ttk.Frame(parent, style="Dark.TFrame")
        viz_frame.pack(side="right", fill="both", expand=True)

        # Notebook with tabs
        self.notebook = ttk.Notebook(viz_frame, style="Dark.TNotebook")
        self.notebook.pack(fill="both", expand=True)

        # Tab 1: 3D Surface Temperature
        self.tab_3d = ttk.Frame(self.notebook, style="Dark.TFrame")
        self.notebook.add(self.tab_3d, text="  🌡️ 3D Surface  ")

        # Tab 2: XZ Cross-Section
        self.tab_xz = ttk.Frame(self.notebook, style="Dark.TFrame")
        self.notebook.add(self.tab_xz, text="  📐 XZ Section  ")

        # Tab 3: YZ Cross-Section
        self.tab_yz = ttk.Frame(self.notebook, style="Dark.TFrame")
        self.notebook.add(self.tab_yz, text="  📐 YZ Section  ")

        # Tab 4: Fluence Map
        self.tab_fluence = ttk.Frame(self.notebook, style="Dark.TFrame")
        self.notebook.add(self.tab_fluence, text="  🔥 Fluence Map  ")

        # Tab 5: Depth Profile
        self.tab_depth = ttk.Frame(self.notebook, style="Dark.TFrame")
        self.notebook.add(self.tab_depth, text="  📏 Depth Profile  ")

        # Create initial placeholder figures
        self._figures = {}
        self._canvases = {}
        self._toolbars = {}

        for tab_name, tab_widget in [
            ("3d", self.tab_3d),
            ("xz", self.tab_xz),
            ("yz", self.tab_yz),
            ("fluence", self.tab_fluence),
            ("depth", self.tab_depth),
        ]:
            fig = Figure(figsize=(8, 6), facecolor=BG_DARK)
            ax = fig.add_subplot(111, projection="3d" if tab_name == "3d" else None)
            ax.set_facecolor(BG_DARK)
            ax.set_title("Run a simulation to see results", color=FG_TEXT, fontsize=12)
            if tab_name != "3d":
                ax.tick_params(colors=FG_TEXT)

            canvas = FigureCanvasTkAgg(fig, master=tab_widget)
            canvas.draw()

            toolbar = NavigationToolbar2Tk(canvas, tab_widget)
            toolbar.update()
            toolbar.pack(side="bottom", fill="x")
            canvas.get_tk_widget().pack(fill="both", expand=True)

            self._figures[tab_name] = fig
            self._canvases[tab_name] = canvas
            self._toolbars[tab_name] = toolbar

    # ================================================================
    # Helpers
    # ================================================================

    def _section_header(self, parent, text):
        ttk.Label(parent, text=text, style="Header.TLabel").pack(
            padx=10, pady=(12, 2), anchor="w",
        )

    def _separator(self, parent):
        sep = tk.Frame(parent, height=1, bg="#333355")
        sep.pack(fill="x", padx=10, pady=8)

    def _on_material_change(self, event=None):
        self._update_material_info()

    def _update_material_info(self):
        name = self.material_var.get()
        mat = MATERIAL_PRESETS.get(name)
        if mat:
            info = (
                f"k={mat.thermal_conductivity} W/mK  |  "
                f"ρ={mat.density} kg/m³\n"
                f"T_melt={mat.t_melt}°C  |  "
                f"α={mat.absorption}"
            )
            self.mat_info_label.config(text=info)

    def _set_status(self, text, color=FG_TEXT):
        self.status_label.config(text=text, foreground=color)
        self.root.update_idletasks()

    def _update_results(self, result: SimulationResult):
        self.results_text.config(state="normal")
        self.results_text.delete("1.0", "end")

        lines = [
            f"Peak Temp:      {result.peak_temperature_celsius:.1f} °C",
            f"Peak Fluence:   {result.peak_fluence_j_per_m2:.2e} J/m²",
            f"Total Energy:   {result.total_energy_j:.4f} J",
            f"Solver Time:    {result.duration_seconds:.2f} s",
            "",
        ]

        if result.melt_depth_m is not None:
            lines.append(f"Melt Depth:     {result.melt_depth_m*1e6:.1f} µm")
        else:
            lines.append("Melt Depth:     None")

        if result.vaporization_depth_m is not None:
            lines.append(f"Vap. Depth:     {result.vaporization_depth_m*1e6:.1f} µm")

        lines.append(f"\nMaterial:       {result.material_name}")
        lines.append(f"Solver:         {result.solver_name}")

        self.results_text.insert("1.0", "\n".join(lines))
        self.results_text.config(state="disabled")

    # ================================================================
    # Simulation Execution
    # ================================================================

    def _run_simulation(self):
        if self._running:
            return

        self._running = True
        self.run_btn.config(text="⏳ RUNNING...", bg="#555577", state="disabled")
        self._set_status("Simulation running... please wait.", FG_ACCENT)

        # Run in background thread to keep GUI responsive
        thread = threading.Thread(target=self._simulation_worker, daemon=True)
        thread.start()

    def _simulation_worker(self):
        try:
            # --- Build dependencies from GUI state ---
            material = MATERIAL_PRESETS[self.material_var.get()]

            beam_key = BEAM_PROFILES[self.beam_var.get()]
            if beam_key == "gaussian":
                laser_source = GaussianLaserSource()
            else:
                laser_source = TopHatLaserSource()

            solver_key = SOLVER_TYPES[self.solver_var.get()]
            if solver_key == "fdm3d":
                solver = FDMHeatSolver3D(laser_source=laser_source)
            else:
                solver = AnalyticalHeatSolver(laser_source=laser_source)

            motion_key = MOTION_PATTERNS[self.motion_var.get()]
            generators = {
                "raster": RasterGenerator(),
                "spiral": SpiralGenerator(),
                "linear": LinearGenerator(),
            }
            motion_gen = generators[motion_key]

            os.makedirs("output", exist_ok=True)
            audit = AuditLogger(log_dir="output")

            use_case = SimulationUseCase(
                heat_solver=solver,
                motion_generator=motion_gen,
                audit_logger=audit,
            )

            laser = LaserBeam(
                power=self.power_var.get(),
                wavelength=1.064e-6,
                spot_radius=self.spot_var.get() * 1e-6,
                focal_length=0.1,
            )

            speed_m_s = self.speed_var.get() / 1000.0
            grid_lx, grid_ly, grid_lz = 0.010, 0.010, 0.002

            if motion_key == "raster":
                motion_params = {
                    "x_start": 0.0, "x_end": grid_lx,
                    "y_start": 0.0, "y_end": grid_ly,
                    "z_focus": 0.05,
                    "line_spacing": 0.5e-3,
                    "scan_speed": speed_m_s,
                }
            elif motion_key == "spiral":
                motion_params = {
                    "center_x": grid_lx / 2, "center_y": grid_ly / 2,
                    "z_focus": 0.05,
                    "inner_radius": 0.5e-3, "outer_radius": 4.0e-3,
                    "n_revolutions": 5,
                    "scan_speed": speed_m_s,
                }
            else:
                motion_params = {
                    "x_start": 0.0, "y_start": grid_ly / 2,
                    "x_end": grid_lx, "y_end": grid_ly / 2,
                    "z_focus": 0.05,
                    "scan_speed": speed_m_s,
                }

            resolution = self.res_var.get() * 1e-6

            result = use_case.execute(
                material=material,
                laser=laser,
                motion_params=motion_params,
                grid_size=(grid_lx, grid_ly, grid_lz),
                resolution=resolution,
            )

            self._result = result

            # Update UI on main thread
            self.root.after(0, self._on_simulation_complete)

        except Exception as exc:
            self.root.after(0, lambda: self._on_simulation_error(str(exc)))

    def _on_simulation_complete(self):
        self._running = False
        self.run_btn.config(text="▶  RUN SIMULATION", bg=BTN_RUN, state="normal")
        self._set_status("✅ Simulation complete!", FG_SUCCESS)
        self._update_results(self._result)
        self._update_all_plots(self._result)

    def _on_simulation_error(self, error_msg):
        self._running = False
        self.run_btn.config(text="▶  RUN SIMULATION", bg=BTN_RUN, state="normal")
        self._set_status(f"❌ Error: {error_msg}", FG_WARN)
        messagebox.showerror("Simulation Error", error_msg)

    # ================================================================
    # Plot Updates
    # ================================================================

    def _update_all_plots(self, result: SimulationResult):
        t_field = np.array(result.temperature_field)

        if t_field.ndim == 3:
            self._plot_3d_surface(result, t_field)
            self._plot_xz_section(result, t_field)
            self._plot_yz_section(result, t_field)
            self._plot_depth_profile(result, t_field)

        fluence = np.array(result.fluence_map)
        if fluence.ndim == 2:
            self._plot_fluence(result, fluence)

    def _plot_3d_surface(self, result, t_field):
        fig = self._figures["3d"]
        fig.clear()

        ax = fig.add_subplot(111, projection="3d")
        ax.set_facecolor("#0a0a1a")
        fig.set_facecolor(BG_DARK)

        x = np.array(result.x_coords) * 1000
        y = np.array(result.y_coords) * 1000
        X, Y = np.meshgrid(x, y, indexing="ij")

        # Surface temperature (z=0 layer)
        surface_temp = t_field[:, :, 0]
        t_ambient = 20.0

        # Normalize for colors
        vmin = t_ambient
        vmax = max(result.peak_temperature_celsius, t_ambient + 100)

        surf = ax.plot_surface(
            X, Y, surface_temp,
            cmap="inferno",
            vmin=vmin, vmax=vmax,
            alpha=0.9,
            edgecolor="none",
            antialiased=True,
        )

        ax.set_xlabel("X [mm]", color=FG_TEXT, fontsize=9, labelpad=8)
        ax.set_ylabel("Y [mm]", color=FG_TEXT, fontsize=9, labelpad=8)
        ax.set_zlabel("Temperature [°C]", color=FG_TEXT, fontsize=9, labelpad=8)
        ax.set_title(
            f"3D Surface Temperature — {result.material_name}",
            color=FG_ACCENT, fontsize=11, fontweight="bold", pad=15,
        )
        ax.tick_params(colors=FG_TEXT, labelsize=7)

        cbar = fig.colorbar(surf, ax=ax, shrink=0.5, pad=0.1)
        cbar.set_label("T [°C]", color=FG_TEXT, fontsize=9)
        cbar.ax.tick_params(colors=FG_TEXT, labelsize=7)

        self._canvases["3d"].draw()

    def _plot_xz_section(self, result, t_field):
        fig = self._figures["xz"]
        fig.clear()
        fig.set_facecolor(BG_DARK)

        ax = fig.add_subplot(111)
        ax.set_facecolor("#0a0a1a")

        x = np.array(result.x_coords) * 1000
        z = np.array(result.z_coords) * 1000
        mid_y = t_field.shape[1] // 2
        cross = t_field[:, mid_y, :].T

        im = ax.pcolormesh(x, z, cross, cmap="hot", shading="auto")
        ax.set_xlabel("X [mm]", color=FG_TEXT, fontsize=11)
        ax.set_ylabel("Depth Z [mm]", color=FG_TEXT, fontsize=11)
        ax.set_title(
            "Temperature — XZ Cross-Section (mid-Y)",
            color=FG_ACCENT, fontsize=12, fontweight="bold",
        )
        ax.invert_yaxis()
        ax.tick_params(colors=FG_TEXT)

        cbar = fig.colorbar(im, ax=ax)
        cbar.set_label("T [°C]", color=FG_TEXT)
        cbar.ax.tick_params(colors=FG_TEXT)
        fig.tight_layout()

        self._canvases["xz"].draw()

    def _plot_yz_section(self, result, t_field):
        fig = self._figures["yz"]
        fig.clear()
        fig.set_facecolor(BG_DARK)

        ax = fig.add_subplot(111)
        ax.set_facecolor("#0a0a1a")

        y = np.array(result.y_coords) * 1000
        z = np.array(result.z_coords) * 1000
        mid_x = t_field.shape[0] // 2
        cross = t_field[mid_x, :, :].T

        im = ax.pcolormesh(y, z, cross, cmap="hot", shading="auto")
        ax.set_xlabel("Y [mm]", color=FG_TEXT, fontsize=11)
        ax.set_ylabel("Depth Z [mm]", color=FG_TEXT, fontsize=11)
        ax.set_title(
            "Temperature — YZ Cross-Section (mid-X)",
            color=FG_ACCENT, fontsize=12, fontweight="bold",
        )
        ax.invert_yaxis()
        ax.tick_params(colors=FG_TEXT)

        cbar = fig.colorbar(im, ax=ax)
        cbar.set_label("T [°C]", color=FG_TEXT)
        cbar.ax.tick_params(colors=FG_TEXT)
        fig.tight_layout()

        self._canvases["yz"].draw()

    def _plot_fluence(self, result, fluence):
        fig = self._figures["fluence"]
        fig.clear()
        fig.set_facecolor(BG_DARK)

        ax = fig.add_subplot(111)
        ax.set_facecolor("#0a0a1a")

        x = np.array(result.x_coords) * 1000
        y = np.array(result.y_coords) * 1000

        im = ax.pcolormesh(x, y, fluence.T, cmap="inferno", shading="auto")
        ax.set_xlabel("X [mm]", color=FG_TEXT, fontsize=11)
        ax.set_ylabel("Y [mm]", color=FG_TEXT, fontsize=11)
        ax.set_title(
            "Cumulative Fluence Map",
            color=FG_ACCENT, fontsize=12, fontweight="bold",
        )
        ax.set_aspect("equal")
        ax.tick_params(colors=FG_TEXT)

        cbar = fig.colorbar(im, ax=ax)
        cbar.set_label("Fluence [J/m²]", color=FG_TEXT)
        cbar.ax.tick_params(colors=FG_TEXT)
        fig.tight_layout()

        self._canvases["fluence"].draw()

    def _plot_depth_profile(self, result, t_field):
        fig = self._figures["depth"]
        fig.clear()
        fig.set_facecolor(BG_DARK)

        ax = fig.add_subplot(111)
        ax.set_facecolor("#0a0a1a")

        z_um = np.array(result.z_coords) * 1e6

        # Find hottest surface point
        surface = t_field[:, :, 0]
        hot_idx = np.unravel_index(np.argmax(surface), surface.shape)
        profile = t_field[hot_idx[0], hot_idx[1], :]

        ax.plot(z_um, profile, color=FG_WARN, linewidth=2.5, label="T(z)")
        ax.fill_between(z_um, 20, profile, alpha=0.15, color=FG_WARN)

        # Material properties
        material = MATERIAL_PRESETS[self.material_var.get()]
        ax.axhline(
            y=material.t_melt, color="#ff9f43", linestyle="--",
            linewidth=1.5, label=f"T_melt = {material.t_melt}°C",
        )
        ax.axhline(
            y=material.t_vaporization, color="#ee5a24", linestyle=":",
            linewidth=1.5, label=f"T_vap = {material.t_vaporization}°C",
        )

        ax.set_xlabel("Depth [µm]", color=FG_TEXT, fontsize=11)
        ax.set_ylabel("Temperature [°C]", color=FG_TEXT, fontsize=11)
        ax.set_title(
            "Depth Temperature Profile (Hottest Point)",
            color=FG_ACCENT, fontsize=12, fontweight="bold",
        )
        ax.legend(fontsize=9, facecolor=BG_PANEL, edgecolor="#444",
                  labelcolor=FG_TEXT)
        ax.tick_params(colors=FG_TEXT)
        ax.grid(True, alpha=0.15, color="#555")
        fig.tight_layout()

        self._canvases["depth"].draw()


# ============================================================================
# Entry Point
# ============================================================================

def main():
    root = tk.Tk()
    app = SimulationGUI(root)
    root.mainloop()


if __name__ == "__main__":
    main()
