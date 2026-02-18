from lab_control.presentation.visualizations import build_3d_lab_figure
try:
    fig = build_3d_lab_figure()
    print("3D Figure Built Successfully")
except Exception as e:
    print(f"Error building 3D figure: {e}")
