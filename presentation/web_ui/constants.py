
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
# Tooltip & Help Styles
# ============================================================================

TOOLTIP_ICON_STYLE = {
    "display": "inline-flex",
    "alignItems": "center",
    "justifyContent": "center",
    "width": "16px",
    "height": "16px",
    "borderRadius": "50%",
    "background": "rgba(100,140,255,0.15)",
    "color": "#64b5f6",
    "fontSize": "10px",
    "fontWeight": "700",
    "cursor": "help",
    "marginLeft": "6px",
    "verticalAlign": "middle",
    "lineHeight": "1",
    "flexShrink": "0",
}

WARNING_STYLE = {
    "display": "flex",
    "alignItems": "center",
    "gap": "8px",
    "padding": "8px 12px",
    "borderRadius": "8px",
    "fontSize": "12px",
    "fontWeight": "500",
    "marginTop": "6px",
    "background": "rgba(255, 165, 0, 0.08)",
    "border": "1px solid rgba(255, 165, 0, 0.2)",
    "color": "#ffb347",
}

DANGER_STYLE = {
    "display": "flex",
    "alignItems": "center",
    "gap": "8px",
    "padding": "8px 12px",
    "borderRadius": "8px",
    "fontSize": "12px",
    "fontWeight": "500",
    "marginTop": "6px",
    "background": "rgba(255, 80, 80, 0.08)",
    "border": "1px solid rgba(255, 80, 80, 0.2)",
    "color": "#ff6b6b",
}

SUCCESS_HINT_STYLE = {
    "display": "flex",
    "alignItems": "center",
    "gap": "8px",
    "padding": "8px 12px",
    "borderRadius": "8px",
    "fontSize": "12px",
    "fontWeight": "500",
    "marginTop": "6px",
    "background": "rgba(80, 200, 100, 0.08)",
    "border": "1px solid rgba(80, 200, 100, 0.2)",
    "color": "#50c864",
}

PRESET_BTN_STYLE = {
    "flex": "1",
    "padding": "10px 8px",
    "background": "rgba(67, 97, 238, 0.08)",
    "color": "#8898c0",
    "border": "1px solid rgba(100, 140, 255, 0.15)",
    "borderRadius": "8px",
    "fontSize": "11px",
    "fontWeight": "600",
    "cursor": "pointer",
    "transition": "all 0.2s ease",
    "textAlign": "center",
    "lineHeight": "1.4",
}

PRESET_BTN_ACTIVE_STYLE = {
    **PRESET_BTN_STYLE,
    "background": "rgba(67, 97, 238, 0.2)",
    "color": "#a0b8ff",
    "borderColor": "rgba(100, 140, 255, 0.35)",
}

BANNER_STYLE = {
    "background": "linear-gradient(135deg, rgba(67,97,238,0.12) 0%, rgba(58,12,163,0.08) 100%)",
    "border": "1px solid rgba(100,140,255,0.15)",
    "borderRadius": "12px",
    "padding": "16px 20px",
    "margin": "0 24px 0 24px",
    "display": "flex",
    "alignItems": "flex-start",
    "justifyContent": "space-between",
    "gap": "16px",
}

BANNER_STEP_STYLE = {
    "display": "flex",
    "alignItems": "center",
    "gap": "8px",
    "color": "#a0b8e0",
    "fontSize": "13px",
    "fontWeight": "500",
}

BANNER_STEP_NUMBER = {
    "display": "inline-flex",
    "alignItems": "center",
    "justifyContent": "center",
    "width": "22px",
    "height": "22px",
    "borderRadius": "50%",
    "background": "rgba(67,97,238,0.25)",
    "color": "#64b5f6",
    "fontSize": "11px",
    "fontWeight": "700",
    "flexShrink": "0",
}

INTERPRETATION_STYLE = {
    "color": "#607898",
    "fontSize": "11px",
    "fontWeight": "400",
    "marginTop": "2px",
    "lineHeight": "1.5",
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
# CSS Injection (App Index String) — Full Mobile-First Responsive Design
# ============================================================================

APP_INDEX_STRING = '''<!DOCTYPE html>
<html>
<head>
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=5.0, user-scalable=yes">
    <meta name="theme-color" content="#0a0e1a">
    <meta name="apple-mobile-web-app-capable" content="yes">
    <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
    {%metas%}{%favicon%}{%css%}
    <title>{%title%}</title>
    <style>
        /* ================================================================
           BASE RESET & GLOBAL
           ================================================================ */
        *, *::before, *::after { box-sizing: border-box; }
        html { -webkit-text-size-adjust: 100%; scroll-behavior: smooth; }
        body {
            margin: 0; padding: 0;
            overscroll-behavior: none;
            -webkit-tap-highlight-color: transparent;
        }

        /* ================================================================
           DASH DROPDOWN — DARK THEME
           ================================================================ */
        .Select-control {
            background: #1a2040 !important;
            border-color: rgba(100,140,255,0.15) !important;
            min-height: 44px !important;  /* Touch-friendly */
        }
        .Select-value-label, .Select-placeholder { color: #c0d0f0 !important; }
        .Select-menu-outer {
            background: #11182e !important;
            border-color: rgba(100,140,255,0.15) !important;
            max-height: 300px !important;
        }
        .VirtualizedSelectOption {
            color: #c0d0f0 !important;
            background: #11182e !important;
            min-height: 40px !important;  /* Touch target */
            display: flex !important;
            align-items: center !important;
        }
        .VirtualizedSelectFocusedOption {
            background: rgba(67,97,238,0.25) !important;
            color: #e0e8ff !important;
        }
        .Select-input > input { color: #c0d0f0 !important; font-size: 16px !important; }
        .Select-arrow { border-color: #506080 transparent transparent !important; }
        .is-open > .Select-control .Select-arrow { border-color: transparent transparent #506080 !important; }
        .Select.is-focused > .Select-control {
            border-color: rgba(67,97,238,0.4) !important;
            box-shadow: 0 0 0 2px rgba(67,97,238,0.15) !important;
        }
        .Select-clear { color: #506080 !important; }
        .Select-noresults { color: #607898 !important; background: #11182e !important; }
        .VirtualizedSelectOption[aria-disabled="true"] {
            color: #4a6090 !important; font-weight: 700 !important; font-size: 10px !important;
            letter-spacing: 1px !important; padding-top: 10px !important;
        }
        .Select-menu-outer ::-webkit-scrollbar { width: 6px; }
        .Select-menu-outer ::-webkit-scrollbar-track { background: #0a1020; }
        .Select-menu-outer ::-webkit-scrollbar-thumb { background: #2a3560; border-radius: 3px; }

        /* ================================================================
           MODAL — DARK THEME
           ================================================================ */
        .modal-content {
            background: #0d1529 !important;
            border: 1px solid rgba(100,140,255,0.1) !important;
        }

        /* ================================================================
           SLIDER — TOUCH FRIENDLY
           ================================================================ */
        .rc-slider-handle {
            width: 24px !important;
            height: 24px !important;
            margin-top: -10px !important;
            border: 2px solid #4361ee !important;
            background: #1a2040 !important;
            box-shadow: 0 2px 8px rgba(67,97,238,0.4) !important;
            opacity: 1 !important;
        }
        .rc-slider-handle:active, .rc-slider-handle:focus {
            box-shadow: 0 0 0 5px rgba(67,97,238,0.25) !important;
        }
        .rc-slider-track { background: #4361ee !important; height: 6px !important; }
        .rc-slider-rail { background: #1a2040 !important; height: 6px !important; }
        .rc-slider-dot { border-color: #2a3560 !important; }
        .rc-slider-mark-text { color: #506080 !important; font-size: 10px !important; }

        /* ================================================================
           TABS — SCROLLABLE ON MOBILE
           ================================================================ */
        .nav-tabs {
            flex-wrap: nowrap !important;
            overflow-x: auto !important;
            -webkit-overflow-scrolling: touch;
            scrollbar-width: none;
            border-bottom: 1px solid rgba(100,140,255,0.1) !important;
        }
        .nav-tabs::-webkit-scrollbar { display: none; }
        .nav-tabs .nav-link {
            white-space: nowrap !important;
            min-height: 44px !important;  /* Touch target */
            display: flex !important;
            align-items: center !important;
            padding: 8px 16px !important;
            color: #607898 !important;
            border: none !important;
            font-size: 12px !important;
            font-weight: 600 !important;
            letter-spacing: 0.3px !important;
            transition: all 0.2s ease !important;
        }
        .nav-tabs .nav-link:hover { color: #a0b8e0 !important; }
        .nav-tabs .nav-link.active {
            color: #64b5f6 !important;
            background: rgba(20, 25, 45, 0.85) !important;
            border-bottom: 2px solid #4361ee !important;
        }

        /* ================================================================
           RADIO ITEMS — TOUCH FRIENDLY
           ================================================================ */
        .form-check { min-height: 40px; display: flex; align-items: center; }
        .form-check-label {
            color: #a0b0d0 !important;
            font-size: 13px !important;
            padding: 8px 4px !important;
            cursor: pointer;
        }
        .form-check-input:checked { background-color: #4361ee !important; border-color: #4361ee !important; }

        /* ================================================================
           BUTTONS — TOUCH FRIENDLY
           ================================================================ */
        button { -webkit-tap-highlight-color: transparent; }
        button:active { transform: scale(0.97); }

        /* ================================================================
           HAMBURGER BUTTON (Desktop: hidden)
           ================================================================ */
        .hamburger-btn {
            display: none;
            background: rgba(67,97,238,0.2);
            border: 1px solid rgba(100,140,255,0.25);
            border-radius: 10px;
            color: #c0d0f0;
            font-size: 22px;
            padding: 8px 14px;
            cursor: pointer;
            transition: all 0.2s ease;
            line-height: 1;
            min-width: 44px;
            min-height: 44px;
        }
        .hamburger-btn:hover { background: rgba(67,97,238,0.4); }

        /* ================================================================
           MOBILE CLOSE BUTTON (Desktop: hidden)
           ================================================================ */
        .mobile-close-btn {
            display: none;
            position: fixed;
            top: 16px;
            right: 16px;
            z-index: 9001;
            background: rgba(255,80,80,0.2);
            border: 1px solid rgba(255,100,100,0.3);
            border-radius: 10px;
            color: #ff6b6b;
            font-size: 20px;
            padding: 8px 14px;
            cursor: pointer;
            transition: all 0.2s ease;
            line-height: 1;
            min-width: 44px;
            min-height: 44px;
        }
        .mobile-close-btn:hover { background: rgba(255,80,80,0.4); }

        /* ================================================================
           DESKTOP LAYOUT (>= 769px)
           ================================================================ */
        @media (min-width: 769px) {
            .mobile-close-btn { display: none !important; }
            .hamburger-btn { display: none !important; }
            /* On desktop: panel is ALWAYS visible, overriding panel-hidden */
            .sim-left-panel,
            .sim-left-panel.panel-hidden,
            .sim-left-panel.panel-visible {
                display: block !important;
                position: static !important;
                transform: none !important;
                opacity: 1 !important;
                pointer-events: auto !important;
                width: auto !important;
                height: auto !important;
                max-height: calc(100vh - 130px) !important;
                z-index: auto !important;
                background: transparent !important;
                backdrop-filter: none !important;
                -webkit-backdrop-filter: none !important;
                padding: 0 !important;
                padding-right: 8px !important;
            }
        }

        /* ================================================================
           MOBILE / TABLET (<= 768px)
           ================================================================ */
        @media (max-width: 768px) {
            .hamburger-btn { display: flex !important; align-items: center; justify-content: center; }
            .mobile-close-btn { display: flex !important; align-items: center; justify-content: center; }

            /* Header compact */
            .sim-header {
                padding: 10px 14px !important;
                flex-wrap: nowrap;
                gap: 8px;
            }
            .sim-header h1 { font-size: 16px !important; letter-spacing: -0.3px !important; }
            .sim-header p { display: none !important; }

            /* Grid becomes single column */
            .sim-main-grid {
                grid-template-columns: 1fr !important;
                max-height: none !important;
                overflow: visible !important;
                padding: 10px !important;
                gap: 10px !important;
            }

            /* Left panel becomes full-screen overlay */
            .sim-left-panel {
                position: fixed !important;
                top: 0 !important;
                left: 0 !important;
                width: 100vw !important;
                height: 100vh !important;
                height: 100dvh !important;  /* Dynamic viewport for mobile browsers */
                max-height: 100vh !important;
                max-height: 100dvh !important;
                z-index: 9000 !important;
                background: rgba(10, 14, 26, 0.98) !important;
                backdrop-filter: blur(24px) !important;
                -webkit-backdrop-filter: blur(24px) !important;
                padding: 16px !important;
                padding-top: 60px !important;
                overflow-y: auto !important;
                -webkit-overflow-scrolling: touch;
                transition: transform 0.35s cubic-bezier(0.22, 1, 0.36, 1),
                            opacity 0.25s ease !important;
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

            /* Right panel (graph) — fills available space */
            .sim-right-panel {
                max-height: none !important;
                min-height: 50vh !important;
                height: calc(100vh - 70px) !important;
                height: calc(100dvh - 70px) !important;
            }

            /* Graph container fills right panel */
            #viz-content {
                flex: 1 !important;
                min-height: 45vh !important;
            }
            .js-plotly-plot, .plotly, .plot-container {
                height: 100% !important;
            }

            /* Cards breathe more on mobile */
            .dash-graph { height: 100% !important; }

            /* Tabs scroll horizontally */
            .nav-tabs .nav-link {
                padding: 10px 14px !important;
                font-size: 11px !important;
            }

            /* Slider marks smaller */
            .rc-slider-mark-text { font-size: 9px !important; }

            /* Modal fullscreen on mobile */
            .modal-dialog {
                max-width: 100vw !important;
                margin: 0 !important;
                height: 100vh !important;
                height: 100dvh !important;
            }
            .modal-content {
                height: 100vh !important;
                height: 100dvh !important;
                border-radius: 0 !important;
                overflow-y: auto !important;
            }
            .modal-body {
                overflow-y: auto !important;
                -webkit-overflow-scrolling: touch;
                padding: 16px !important;
            }
            .modal-header { padding: 12px 16px !important; }
            .modal-footer { padding: 12px 16px !important; }

            /* Status badge smaller */
            #status-badge {
                font-size: 10px !important;
                padding: 4px 10px !important;
            }

            /* Loading overlay */
            .dash-loading { z-index: 10000; }
        }

        /* ================================================================
           SMALL PHONES (<= 400px)
           ================================================================ */
        @media (max-width: 400px) {
            .sim-header h1 { font-size: 14px !important; }
            .nav-tabs .nav-link {
                padding: 8px 10px !important;
                font-size: 10px !important;
            }
            .rc-slider-handle {
                width: 28px !important;
                height: 28px !important;
                margin-top: -12px !important;
            }
        }

        /* ================================================================
           LANDSCAPE MOBILE
           ================================================================ */
        @media (max-width: 768px) and (orientation: landscape) {
            .sim-right-panel {
                height: calc(100vh - 55px) !important;
                height: calc(100dvh - 55px) !important;
                min-height: 200px !important;
            }
            .sim-header { padding: 6px 14px !important; }
            .sim-header h1 { font-size: 14px !important; }
        }

        /* ================================================================
           PRINT — CLEAN OUTPUT
           ================================================================ */
        @media print {
            .hamburger-btn, .mobile-close-btn, #run-btn { display: none !important; }
            .sim-left-panel { display: block !important; position: static !important; }
            body { background: white !important; }
        }

        /* ================================================================
           ANIMATIONS — REDUCED MOTION ACCESSIBILITY
           ================================================================ */
        @media (prefers-reduced-motion: reduce) {
            *, *::before, *::after {
                animation-duration: 0.01ms !important;
                transition-duration: 0.01ms !important;
            }
        }

        /* ================================================================
           RUN BUTTON — PULSE ANIMATION (before first click)
           ================================================================ */
        @keyframes pulse-glow {
            0%, 100% { box-shadow: 0 4px 20px rgba(67,97,238,0.35); }
            50% { box-shadow: 0 4px 30px rgba(67,97,238,0.6), 0 0 40px rgba(67,97,238,0.2); }
        }
        .run-btn-pulse {
            animation: pulse-glow 2s ease-in-out infinite;
        }
        .run-btn-computing {
            opacity: 0.7;
            pointer-events: none;
        }

        /* ================================================================
           CUSTOM WARNING/DANGER ICONS (pure CSS, no emojis)
           ================================================================ */
        .icon-warning, .icon-danger, .icon-success {
            display: inline-flex;
            align-items: center;
            justify-content: center;
            width: 18px;
            height: 18px;
            border-radius: 50%;
            font-size: 11px;
            font-weight: 700;
            flex-shrink: 0;
            line-height: 1;
        }
        .icon-warning {
            background: rgba(255,179,71,0.2);
            color: #ffb347;
            border: 1.5px solid rgba(255,179,71,0.4);
        }
        .icon-danger {
            background: rgba(255,107,107,0.2);
            color: #ff6b6b;
            border: 1.5px solid rgba(255,107,107,0.4);
        }
        .icon-success {
            background: rgba(80,200,100,0.2);
            color: #50c864;
            border: 1.5px solid rgba(80,200,100,0.4);
        }

        /* ================================================================
           TOOLTIP INFO ICON HOVER
           ================================================================ */
        .info-icon {
            transition: all 0.2s ease;
        }
        .info-icon:hover {
            background: rgba(100,140,255,0.3) !important;
            transform: scale(1.1);
        }

        /* ================================================================
           PRESET BUTTONS HOVER
           ================================================================ */
        .preset-btn:hover {
            background: rgba(67,97,238,0.2) !important;
            color: #a0b8ff !important;
            border-color: rgba(100,140,255,0.35) !important;
            transform: translateY(-1px);
        }
        .preset-btn:active {
            transform: scale(0.97);
        }

        /* ================================================================
           BANNER DISMISS
           ================================================================ */
        .getting-started-banner {
            transition: all 0.35s ease;
            overflow: hidden;
        }
        .getting-started-banner.banner-hidden {
            max-height: 0 !important;
            padding: 0 20px !important;
            margin-bottom: 0 !important;
            border-color: transparent !important;
            opacity: 0;
        }

        /* ================================================================
           TAB ICONS (CSS-only indicators)
           ================================================================ */
        .tab-icon {
            display: inline-block;
            width: 8px;
            height: 8px;
            border-radius: 2px;
            margin-right: 6px;
            vertical-align: middle;
        }
        .tab-icon-3d { background: #4361ee; }
        .tab-icon-section { background: #e056fd; }
        .tab-icon-energy { background: #ffb347; }
        .tab-icon-depth { background: #ff6b6b; }
    </style>
</head>
<body>
    {%app_entry%}
    <footer>{%config%}{%scripts%}{%renderer%}</footer>
</body>
</html>'''
