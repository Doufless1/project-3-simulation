
import threading
import webbrowser
from presentation.web_ui.app import app

if __name__ == "__main__":
    port = 8050
    print(f"\n  Laser-HVOF 3D Simulation Lab (Modular)")
    print(f"  -> Opening at http://localhost:{port}\n")
    threading.Timer(1.5, lambda: webbrowser.open(f"http://localhost:{port}")).start()
    app.run(debug=False, port=port)
