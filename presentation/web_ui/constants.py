
import plotly.graph_objects as go

# ============================================================================
# CSS Styles (inline for zero dependencies)
# ============================================================================

CARD_STYLE = {
    "background": "rgba(20, 25, 45, 0.85)",
    "backdropFilter": "blur(12px)",
    "border": "1px solid rgba(100, 140, 255, 0.12)",
    "borderRadius": "14px",
    "padding": "20px",
    "marginBottom": "16px",
    "boxShadow": "0 4px 30px rgba(0,0,0,0.3)",
}

SECTION_TITLE_STYLE = {
    "color": "#64b5f6",
    "fontSize": "13px",
    "fontWeight": "700",
    "textTransform": "uppercase",
    "letterSpacing": "1.5px",
    "marginBottom": "14px",
}

SLIDER_LABEL_STYLE = {
    "color": "#8898c0",
    "fontSize": "12px",
    "fontWeight": "500",
    "marginBottom": "2px",
    "marginTop": "10px",
}

VALUE_STYLE = {
    "color": "#e0e8ff",
    "fontSize": "22px",
    "fontWeight": "700",
    "fontFamily": "JetBrains Mono, Consolas, monospace",
}

RESULT_CARD_STYLE = {
    **CARD_STYLE,
    "background": "rgba(20, 30, 55, 0.9)",
    "border": "1px solid rgba(100, 200, 255, 0.15)",
}

METRIC_LABEL = {
    "color": "#7088b0",
    "fontSize": "11px",
    "fontWeight": "600",
    "textTransform": "uppercase",
    "letterSpacing": "0.8px",
}

METRIC_VALUE = {
    "color": "#e0f0ff",
    "fontSize": "20px",
    "fontWeight": "700",
    "fontFamily": "JetBrains Mono, Consolas, monospace",
}

# ============================================================================
# Plotly Dark Theme Template
# ============================================================================

PLOT_TEMPLATE = dict(
    layout=go.Layout(
        paper_bgcolor="rgba(0,0,0,0)",
        plot_bgcolor="rgba(10,10,25,0.85)",
        font=dict(color="#c0c8e0", family="Inter, system-ui, sans-serif"),
        margin=dict(l=50, r=30, t=50, b=50),
        coloraxis=dict(colorbar=dict(
            tickfont=dict(color="#c0c8e0"),
            title=dict(font=dict(color="#c0c8e0")),
        )),
    )
)

# ============================================================================
# CSS Injection (App Index String)
# ============================================================================

APP_INDEX_STRING = '''<!DOCTYPE html>
<html>
<head>
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
    {%metas%}{%favicon%}{%css%}
    <title>{%title%}</title>
    <style>
        /* Dash dropdown dark theme */
        .Select-control { background: #1a2040 !important; border-color: rgba(100,140,255,0.15) !important; }
        .Select-value-label, .Select-placeholder { color: #c0d0f0 !important; }
        .Select-menu-outer { background: #11182e !important; border-color: rgba(100,140,255,0.15) !important; }
        .VirtualizedSelectOption { color: #c0d0f0 !important; background: #11182e !important; }
        .VirtualizedSelectFocusedOption { background: rgba(67,97,238,0.25) !important; color: #e0e8ff !important; }
        .Select-input > input { color: #c0d0f0 !important; }
        .Select-arrow { border-color: #506080 transparent transparent !important; }
        .is-open > .Select-control .Select-arrow { border-color: transparent transparent #506080 !important; }
        .Select.is-focused > .Select-control { border-color: rgba(67,97,238,0.4) !important; box-shadow: 0 0 0 2px rgba(67,97,238,0.15) !important; }
        .Select-clear { color: #506080 !important; }
        .Select-noresults { color: #607898 !important; background: #11182e !important; }
        /* Disabled option (headers) */
        .VirtualizedSelectOption[aria-disabled="true"] {
            color: #4a6090 !important; font-weight: 700 !important; font-size: 10px !important;
            letter-spacing: 1px !important; padding-top: 10px !important;
        }
        /* Scrollbar */
        .Select-menu-outer ::-webkit-scrollbar { width: 6px; }
        .Select-menu-outer ::-webkit-scrollbar-track { background: #0a1020; }
        .Select-menu-outer ::-webkit-scrollbar-thumb { background: #2a3560; border-radius: 3px; }
        /* Modal dark theme */
        .modal-content { background: #0d1529 !important; border: 1px solid rgba(100,140,255,0.1) !important; }

        /* ---- Hamburger Button ---- */
        .hamburger-btn {
            display: none;  /* Hidden on desktop */
            background: rgba(67,97,238,0.2);
            border: 1px solid rgba(100,140,255,0.25);
            border-radius: 10px;
            color: #c0d0f0;
            font-size: 22px;
            padding: 6px 12px;
            cursor: pointer;
            transition: all 0.2s ease;
            line-height: 1;
        }
        .hamburger-btn:hover { background: rgba(67,97,238,0.4); }

        /* ---- Responsive Mobile Layout ---- */
        @media (max-width: 768px) {
            .hamburger-btn { display: block !important; }

            .sim-main-grid {
                grid-template-columns: 1fr !important;
                max-height: none !important;
                overflow: visible !important;
                padding: 12px !important;
                gap: 12px !important;
            }
            .sim-left-panel {
                position: fixed !important;
                top: 0 !important;
                left: 0 !important;
                width: 100vw !important;
                height: 100vh !important;
                max-height: 100vh !important;
                z-index: 9000 !important;
                background: rgba(10, 14, 26, 0.97) !important;
                backdrop-filter: blur(20px) !important;
                padding: 20px !important;
                padding-top: 70px !important;
                overflow-y: auto !important;
                transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1),
                            opacity 0.3s ease !important;
            }
            .sim-left-panel.panel-hidden {
                transform: translateX(-100%) !important;
                opacity: 0 !important;
                pointer-events: none !important;
            }
            .sim-left-panel.panel-visible {
                transform: translateX(0) !important;
                opacity: 1 !important;
                pointer-events: auto !important;
            }
            /* Close button inside the mobile panel */
            .mobile-close-btn {
                position: fixed;
                top: 16px;
                right: 16px;
                z-index: 9001;
                background: rgba(255,80,80,0.2);
                border: 1px solid rgba(255,100,100,0.3);
                border-radius: 10px;
                color: #ff6b6b;
                font-size: 20px;
                padding: 6px 14px;
                cursor: pointer;
                transition: all 0.2s ease;
                line-height: 1;
            }
            .mobile-close-btn:hover { background: rgba(255,80,80,0.4); }

            .sim-right-panel {
                max-height: 60vh !important;
                min-height: 300px;
            }
            .sim-header {
                padding: 12px 16px !important;
                flex-wrap: wrap;
                gap: 8px;
            }
            .sim-header h1 { font-size: 18px !important; }
        }

        /* Desktop: hide mobile-only elements */
        @media (min-width: 769px) {
            .mobile-close-btn { display: none !important; }
        }
    </style>
</head>
<body>
    {%app_entry%}
    <footer>{%config%}{%scripts%}{%renderer%}</footer>
</body>
</html>'''
