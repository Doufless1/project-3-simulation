import os
import json
import sys

# Ensure domain entities can be imported
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../")))

from domain.entities import Material

# Element property database for estimating thermal properties from composition
ELEMENT_PROPS = {
    "W":  {"k": 173.0, "rho": 19250, "cp": 134, "abs": 0.50},
    "C":  {"k": 140.0, "rho":  2260, "cp": 709, "abs": 0.85},
    "Ta": {"k":  57.5, "rho": 16690, "cp": 140, "abs": 0.60},
    "Cr": {"k":  93.9, "rho":  7190, "cp": 449, "abs": 0.55},
    "Fe": {"k":  80.4, "rho":  7874, "cp": 449, "abs": 0.45},
    "Ni": {"k":  90.9, "rho":  8908, "cp": 444, "abs": 0.40},
    "Co": {"k": 100.0, "rho":  8900, "cp": 421, "abs": 0.45},
    "Mo": {"k": 138.0, "rho": 10220, "cp": 251, "abs": 0.55},
    "Ti": {"k":  21.9, "rho":  4507, "cp": 523, "abs": 0.60},
    "Nb": {"k":  53.7, "rho":  8570, "cp": 265, "abs": 0.55},
    "Hf": {"k":  23.0, "rho": 13310, "cp": 144, "abs": 0.55},
    "V":  {"k":  30.7, "rho":  6110, "cp": 489, "abs": 0.55},
    "Zr": {"k":  22.7, "rho":  6520, "cp": 278, "abs": 0.55},
    "Al": {"k": 237.0, "rho":  2700, "cp": 897, "abs": 0.30},
    "Cu": {"k": 401.0, "rho":  8960, "cp": 385, "abs": 0.25},
    "Mn": {"k":   7.8, "rho":  7210, "cp": 479, "abs": 0.55},
    "Si": {"k": 149.0, "rho":  2330, "cp": 712, "abs": 0.65},
}


def estimate_thermal_props(composition: dict) -> dict:
    """Estimate thermal properties from composition using rule-of-mixtures."""
    k, rho, cp, absorption = 0.0, 0.0, 0.0, 0.0
    total = sum(composition.values())
    if total <= 0:
        return {"k": 50.0, "rho": 8000.0, "cp": 400.0, "abs": 0.5}
    for elem, frac in composition.items():
        w = frac / total
        props = ELEMENT_PROPS.get(elem, {"k": 50, "rho": 8000, "cp": 400, "abs": 0.5})
        k += w * props["k"]
        rho += w * props["rho"]
        cp += w * props["cp"]
        absorption += w * props["abs"]
    return {"k": round(k, 1), "rho": round(rho, 1), "cp": round(cp, 1), "abs": round(min(absorption, 1.0), 3)}


def format_composition_str(composition: dict) -> str:
    """Format composition dict as a readable string."""
    parts = []
    for elem, frac in sorted(composition.items(), key=lambda x: -x[1]):
        pct = frac * 100
        if pct >= 0.1:
            parts.append(f"{elem} {pct:.1f}%")
    return " · ".join(parts)


def load_materials() -> dict:
    """Load all materials from discovered_materials.json."""
    materials = {}

    # Standard materials (always available)
    materials["ss316l"] = {
        "label": "316L Stainless Steel",
        "category": "standard",
        "obj": Material(
            name="316L Stainless Steel",
            absorption=0.35, thermal_conductivity=15.0,
            density=8000.0, specific_heat=500.0,
            t_ambient=20.0, t_melt=1400.0, t_vaporization=2800.0,
        ),
        "composition": "Fe 65% · Cr 17% · Ni 12% · Mo 2.5%",
    }
    materials["wc_co"] = {
        "label": "WC-12Co (HVOF)",
        "category": "standard",
        "obj": Material(
            name="WC-12Co (HVOF Coating)",
            absorption=0.55, thermal_conductivity=80.0,
            density=14500.0, specific_heat=240.0,
            t_ambient=20.0, t_melt=2870.0, t_vaporization=6000.0,
        ),
        "composition": "WC 88% · Co 12%",
    }
    materials["inconel718"] = {
        "label": "Inconel 718",
        "category": "standard",
        "obj": Material(
            name="Inconel 718",
            absorption=0.38, thermal_conductivity=11.4,
            density=8190.0, specific_heat=435.0,
            t_ambient=20.0, t_melt=1260.0, t_vaporization=2700.0,
        ),
        "composition": "Ni 53% · Cr 19% · Fe 18% · Nb 5% · Mo 3%",
    }
    materials["ti6al4v"] = {
        "label": "Ti-6Al-4V",
        "category": "standard",
        "obj": Material(
            name="Ti-6Al-4V",
            absorption=0.42, thermal_conductivity=6.7,
            density=4430.0, specific_heat=526.0,
            t_ambient=20.0, t_melt=1660.0, t_vaporization=3290.0,
        ),
        "composition": "Ti 90% · Al 6% · V 4%",
    }

    # Load discovered materials from JSON
    # Adjusted paths to look in project root from presentation/web_ui/services/
    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../"))
    json_paths = [
        os.path.join(root_dir, "discovered_materials.json"),
        os.path.join(root_dir, "discovery", "discovered_materials.json"),
    ]

    loaded_names = set()
    for jpath in json_paths:
        if not os.path.exists(jpath):
            continue
        try:
            with open(jpath, "r", encoding="utf-8") as f:
                data = json.load(f)

            for entry in data:
                name = entry.get("name", "Unknown")
                if name in loaded_names:
                    continue
                loaded_names.add(name)

                comp = entry.get("composition", {})
                props = entry.get("properties", {})

                # Get thermal properties — use from JSON or estimate
                t_melt = props.get("T_melt_C", props.get("T_melt", 2500.0))
                t_vap = props.get("T_vap_C", props.get("T_vap", t_melt + 1500.0))
                density = props.get("Density_kg_m3", None)

                estimated = estimate_thermal_props(comp)
                if density is None:
                    density = estimated["rho"]

                # Create safe key
                key = name.lower().replace("-", "_").replace(" ", "_")

                materials[key] = {
                    "label": name,
                    "category": "discovered",
                    "score": props.get("Score", props.get("fitness_score", 0)),
                    "hardness": props.get("Hardness_HV", 0),
                    "obj": Material(
                        name=name,
                        absorption=estimated["abs"],
                        thermal_conductivity=estimated["k"],
                        density=density,
                        specific_heat=estimated["cp"],
                        t_ambient=20.0,
                        t_melt=t_melt,
                        t_vaporization=max(t_vap, t_melt + 100),
                    ),
                    "composition": format_composition_str(comp),
                }
        except Exception as e:
            print(f"Warning: Could not load {jpath}: {e}")

    return materials


def build_material_options(materials_dict: dict) -> list:
    """Build grouped dropdown options: Discovered (sorted by score) + Standard."""
    discovered = []
    standard = []
    custom = []
    
    for k, v in materials_dict.items():
        entry = {"label": v["label"], "value": k}
        cat = v.get("category")
        if cat == "discovered":
            discovered.append((v.get("score", 0), entry))
        elif cat == "custom":
            custom.append(entry)
        else:
            standard.append(entry)

    # Sort discovered by score descending
    discovered.sort(key=lambda x: -x[0])

    options = []
    if discovered:
        options.append({"label": "--- AI-Discovered Materials ---", "value": "__header_discovered__", "disabled": True})
        for _, entry in discovered:
            options.append(entry)
    if custom:
        options.append({"label": "--- Custom Materials ---", "value": "__header_custom__", "disabled": True})
        for entry in custom:
            options.append(entry)
    if standard:
        options.append({"label": "--- Standard Materials ---", "value": "__header_standard__", "disabled": True})
        for entry in standard:
            options.append(entry)
            
    return options

# Global instance for easy import
MATERIALS = load_materials()
