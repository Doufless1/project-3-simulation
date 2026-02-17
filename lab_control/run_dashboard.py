"""
Entry Point — Run the Virtual Lab Control Dashboard.

Usage:
    python -m lab_control.run_dashboard

Or:
    python lab_control/run_dashboard.py
"""

import sys
import os

# Ensure project root is on path
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from lab_control.presentation.dashboard import run


if __name__ == "__main__":
    print("\n" + "=" * 60)
    print("  HVOF Laser Lab - Virtual Control Dashboard")
    print("  Clean Architecture - CIA Triad - STRIDE")
    print("=" * 60)
    print("  Starting at: http://127.0.0.1:8051")
    print("  Press Ctrl+C to stop")
    print("=" * 60 + "\n")
    run()
