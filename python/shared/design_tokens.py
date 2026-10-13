"""
Unified Design Tokens — Single source of truth for both apps.

All colors, spacing, typography, and reusable component styles live here.
Both Lab Control and Simulation apps import from this module to guarantee
visual consistency across the platform.
"""

# ── Color Palette ────────────────────────────────────────────────────
# Based on Tailwind Slate + custom accents. Meets WCAG AA contrast on
# dark backgrounds for all text colors.

COLORS = {
    # Backgrounds
    "bg": "#0a0e17",
    "bg_subtle": "#0d1529",
    "card": "#111827",
    "card_border": "#1e293b",

    # Brand / Interactive
    "primary": "#3b82f6",
    "primary_hover": "#2563eb",
    "accent": "#8b5cf6",

    # Semantic
    "success": "#10b981",
    "danger": "#ef4444",
    "warning": "#f59e0b",

    # Text — all meet WCAG AA on #0a0e17 / #111827
    "text": "#e2e8f0",          # Primary text — 13.5:1 on bg
    "text_secondary": "#94a3b8", # Secondary — 5.5:1 on bg (AA pass)
    "muted": "#a0aec0",         # Muted — 6.3:1 on bg (AA pass, was #94a3b8)
    "muted_strong": "#cbd5e1",  # Emphasized muted — 10:1
}

# ── Typography ───────────────────────────────────────────────────────

FONT_FAMILY = "'Inter', 'Segoe UI', system-ui, -apple-system, sans-serif"
FONT_MONO = "'JetBrains Mono', 'Fira Code', 'Consolas', monospace"

# ── Shared Component Styles ──────────────────────────────────────────

CARD_STYLE = {
    "backgroundColor": COLORS["card"],
    "border": f"1px solid {COLORS['card_border']}",
    "borderRadius": "12px",
    "padding": "20px",
    "marginBottom": "16px",
}

LABEL_STYLE = {
    "color": COLORS["muted"],
    "fontSize": "12px",
    "textTransform": "uppercase",
    "letterSpacing": "1px",
    "marginBottom": "6px",
    "fontWeight": "600",
}

BTN_STYLE = {
    "padding": "10px 20px",
    "borderRadius": "8px",
    "border": "none",
    "cursor": "pointer",
    "fontWeight": "600",
    "fontSize": "13px",
    "marginRight": "8px",
    "marginBottom": "8px",
    "display": "inline-flex",
    "alignItems": "center",
    "gap": "8px",
    "transition": "opacity 0.2s, background-color 0.2s",
    "fontFamily": FONT_FAMILY,
}

SLIDER_MARKS_STYLE = {"color": COLORS["muted"], "fontSize": "11px"}

INFO_ICON_STYLE = {
    "display": "inline-flex",
    "alignItems": "center",
    "justifyContent": "center",
    "width": "18px",
    "height": "18px",
    "borderRadius": "50%",
    "backgroundColor": "rgba(139, 92, 246, 0.15)",
    "color": COLORS["accent"],
    "fontSize": "11px",
    "fontWeight": "700",
    "cursor": "help",
    "marginLeft": "6px",
    "flexShrink": "0",
}

TOOLTIP_STYLE = {
    "position": "relative",
    "display": "inline-flex",
    "alignItems": "center",
}

TAB_INTRO_STYLE = {
    "backgroundColor": "rgba(59, 130, 246, 0.08)",
    "border": "1px solid rgba(59, 130, 246, 0.2)",
    "borderRadius": "8px",
    "padding": "12px 16px",
    "marginBottom": "16px",
    "fontSize": "13px",
    "color": COLORS["muted"],
    "lineHeight": "1.5",
}

BANNER_STYLE = {
    "background": "linear-gradient(135deg, rgba(59,130,246,0.12) 0%, rgba(139,92,246,0.12) 100%)",
    "border": "1px solid rgba(59, 130, 246, 0.25)",
    "borderRadius": "12px",
    "padding": "20px",
    "marginBottom": "20px",
}

# App description banner — "What does this app do?"
APP_DESCRIPTION_STYLE = {
    "backgroundColor": "rgba(59, 130, 246, 0.06)",
    "border": "1px solid rgba(59, 130, 246, 0.15)",
    "borderRadius": "10px",
    "padding": "14px 20px",
    "marginBottom": "16px",
    "display": "flex",
    "alignItems": "center",
    "justifyContent": "space-between",
    "gap": "16px",
}

# Inline contextual warning/error near action buttons
INLINE_WARNING_STYLE = {
    "display": "flex",
    "alignItems": "center",
    "gap": "8px",
    "padding": "8px 14px",
    "borderRadius": "8px",
    "fontSize": "12px",
    "fontWeight": "500",
    "marginTop": "8px",
    "background": "rgba(245, 158, 11, 0.08)",
    "border": "1px solid rgba(245, 158, 11, 0.2)",
    "color": COLORS["warning"],
}

INLINE_ERROR_STYLE = {
    "display": "flex",
    "alignItems": "center",
    "gap": "8px",
    "padding": "8px 14px",
    "borderRadius": "8px",
    "fontSize": "12px",
    "fontWeight": "500",
    "marginTop": "8px",
    "background": "rgba(239, 68, 68, 0.08)",
    "border": "1px solid rgba(239, 68, 68, 0.2)",
    "color": COLORS["danger"],
}

INLINE_SUCCESS_STYLE = {
    "display": "flex",
    "alignItems": "center",
    "gap": "8px",
    "padding": "8px 14px",
    "borderRadius": "8px",
    "fontSize": "12px",
    "fontWeight": "500",
    "marginTop": "8px",
    "background": "rgba(16, 185, 129, 0.08)",
    "border": "1px solid rgba(16, 185, 129, 0.2)",
    "color": COLORS["success"],
}

# Workflow stepper styles
STEP_NUMBER_STYLE = {
    "width": "28px",
    "height": "28px",
    "borderRadius": "50%",
    "display": "flex",
    "alignItems": "center",
    "justifyContent": "center",
    "fontSize": "13px",
    "fontWeight": "700",
    "flexShrink": "0",
    "color": "#fff",
}

STEP_COMPLETE_STYLE = {
    **STEP_NUMBER_STYLE,
    "backgroundColor": COLORS["success"],
}

STEP_ACTIVE_STYLE = {
    **STEP_NUMBER_STYLE,
    "backgroundColor": COLORS["primary"],
}

STEP_PENDING_STYLE = {
    **STEP_NUMBER_STYLE,
    "backgroundColor": COLORS["card_border"],
}

# Disabled button overlay style
BTN_DISABLED_STYLE = {
    **BTN_STYLE,
    "opacity": "0.4",
    "cursor": "not-allowed",
    "pointerEvents": "none",
}

# Section title (used for card headers)
SECTION_TITLE_STYLE = {
    "color": COLORS["primary"],
    "fontSize": "13px",
    "fontWeight": "700",
    "textTransform": "uppercase",
    "letterSpacing": "1.5px",
    "marginBottom": "14px",
}

# Value display (metrics, readings)
VALUE_STYLE = {
    "color": COLORS["text"],
    "fontSize": "22px",
    "fontWeight": "700",
    "fontFamily": FONT_MONO,
}

# Result card (variation of CARD_STYLE)
RESULT_CARD_STYLE = {
    **CARD_STYLE,
    "background": "rgba(17, 24, 39, 0.95)",
    "border": f"1px solid rgba(59, 130, 246, 0.15)",
}

# Preset button styles (simulation app)
PRESET_BTN_STYLE = {
    "flex": "1",
    "padding": "10px 8px",
    "background": f"rgba(59, 130, 246, 0.08)",
    "color": COLORS["muted"],
    "border": f"1px solid rgba(59, 130, 246, 0.15)",
    "borderRadius": "8px",
    "fontSize": "11px",
    "fontWeight": "600",
    "cursor": "pointer",
    "transition": "all 0.2s ease",
    "textAlign": "center",
    "lineHeight": "1.4",
    "fontFamily": FONT_FAMILY,
}

PRESET_BTN_ACTIVE_STYLE = {
    **PRESET_BTN_STYLE,
    "background": f"rgba(59, 130, 246, 0.2)",
    "color": COLORS["text_secondary"],
    "borderColor": f"rgba(59, 130, 246, 0.35)",
}
