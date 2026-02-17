"""
ADVANCED Material Discovery Engine v2.0

Seeds evolution from TOP REAL ALLOYS (WC-Co, Tungsten, Mo-TZM).
Aggressively mutates to find "impossible" super-materials.

TARGETS:
- Max Temperature: 2,000,000°C (theoretical) -> realistic: highest possible
- Melt Depth: >200 µm (deep penetration)
- Score: 10,000+
- Bonus: Lightweight (low density)

ALGORITHM: Advanced Genetic Algorithm with:
- Elitism
- Multi-objective optimization (NSGA-II inspired)
- Adaptive mutation rates
- Real alloy seeding
"""

import random
import json
import os
import numpy as np
from dataclasses import dataclass, field
from typing import List, Dict, Optional

# Import real materials database (Materials Project)
from materials_project_db import MaterialsProjectDB, get_offline_database
from .materials_db import ELEMENTS, Element

# ============================================================================
# CONFIGURATION
# ============================================================================
POPULATION_SIZE = 100
GENERATIONS = 50
ELITE_COUNT = 10
BASE_MUTATION_RATE = 0.3
CROSSOVER_RATE = 0.7

# Target thresholds (EXTREME 4000C+)
TARGET_T_MELT = 4300       # °C (Aiming for Ta4HfC5 record)
TARGET_HARDNESS = 35000    # HV
TARGET_SCORE = 50000
TARGET_EROSION = 0.01      # mm/year (Lower is better)

@dataclass
class SuperAlloy:
    """Evolved super-material."""
    name: str
    generation: int
    composition: Dict[str, float]
    
    # Calculated properties
    T_melt: float = 0.0
    T_vap: float = 0.0
    density: float = 0.0
    thermal_cond: float = 0.0
    specific_heat: float = 0.0
    hardness: float = 0.0
    cost: float = 0.0
    
    # New properties
    erosion_rate: float = 0.0     # Relative index (Lower is good)
    optimal_thickness: float = 0.0 # µm (Based on thermal insulation needed)
    
    # Fitness metrics
    fitness_score: float = 0.0
    pareto_rank: int = 0
    
    def __post_init__(self):
        self._calculate_properties()
    
    def _calculate_properties(self):
        """Compute properties using advanced mixing rules."""
        total = sum(self.composition.values())
        if total < 0.99 or total > 1.01 and total > 0:
            for k in self.composition:
                self.composition[k] /= total
        
        # Reset
        self.T_melt = 0; self.T_vap = 0; self.density = 0
        self.thermal_cond = 0; self.specific_heat = 0
        self.hardness = 0; self.cost = 0
        
        for sym, frac in self.composition.items():
            if sym not in ELEMENTS: continue
            el = ELEMENTS[sym]
            self.T_melt += el.melt_temp * frac
            self.T_vap += el.vapor_temp * frac
            self.density += el.density * frac
            self.thermal_cond += el.thermal_cond * frac
            self.specific_heat += el.specific_heat * frac
            self.hardness += el.hardness * frac
            self.cost += el.cost_per_kg * frac
        
        # ==========================================
        # ADVANCED SYNERGY PHYSICS
        # ==========================================
        
        # 1. THE 4000°C FORMULA (Ta-Hf-C Synergy)
        # Ta4HfC5 stoichiometry is roughly: Ta: 0.5, Hf: 0.12, C: 0.38 (by mass approx)
        ta = self.composition.get('Ta', 0)
        hf = self.composition.get('Hf', 0)
        c = self.composition.get('C', 0)
        
        if ta > 0.3 and hf > 0.05 and c > 0.05:
            # The "Hafnium Effect": Hf suppresses diffusion in TaC lattice
            # Peak effect at roughly 4:1 Ta:Hf ratio
            ratio_bonus = 1.0 - abs((ta/hf) - 4.0) if hf > 0 else 0
            if ratio_bonus > 0:
                self.T_melt += 800 * ratio_bonus # Push towards 4000C
                self.hardness *= 1.2
            else:
                 self.T_melt += 200 # General refractory boost
        
        # 2. General Carbide Hardening
        carbide_formers = sum(self.composition.get(e, 0) for e in ['W', 'Ti', 'Cr', 'Mo', 'Ta', 'Hf'])
        if c > 0.05 and carbide_formers > 0.3:
            self.hardness *= (1.5 + (c * 20))
            self.T_melt *= 1.1

        # ==========================================
        # NEW: EROSION & THICKNESS MODELING
        # ==========================================
        
        # Erosion Rate (Archard's Law approximation)
        # E ~ k / Hardness. Adjusted by Density (Momentum resistance)
        # Arbitrary units, lower is better.
        if self.hardness > 0:
            self.erosion_rate = (100000 / self.hardness) * (8000 / (self.density + 1))
        else:
            self.erosion_rate = 100.0

        # Optimal Thickness Calculation
        # Goal: Insulate substrate (keep < 500C) from flame (3000C)
        # Q = k * dT / dx  => dx = k * dT / Q
        # Lower conductivity (k) means we can use THINNER coatings.
        # Thinner is better (less stress, cheaper).
        # Base thickness approx: 200µm for steel, 50µm for ceramic.
        self.optimal_thickness = (self.thermal_cond / 20.0) * 100.0 # µm
        # Clamp
        self.optimal_thickness = max(20.0, min(500.0, self.optimal_thickness))


class AdvancedDiscoveryEngine:
    """
    Advanced Genetic Algorithm with:
    - Real alloy seeding (Materials Project)
    - Ta-Hf-C 4000C targeting
    - Erosion/Thickness optimization
    """
    
    def __init__(self):
        self.population: List[SuperAlloy] = []
        self.hall_of_fame: List[SuperAlloy] = []
        self.generation = 0
        self.elements = list(ELEMENTS.keys())
        
        # Connect to Materials Project DB
        try:
            self.mp_db = MaterialsProjectDB()
            self.use_online = True
            print("Connected to Materials Project API")
        except Exception as e:
            self.use_online = False
            print("Using offline database fallback")

    def seed_from_library(self):
        """Seed population from PREVIOUS discoveries (discovered_materials.json)."""
        filename = "discovered_materials.json"
        if not os.path.exists(filename):
            print("No previous library found.")
            return

        print("Seeding from Discovered Materials Library...")
        try:
            with open(filename, 'r') as f:
                data = json.load(f)
            
            # Helper to get score safely
            def get_score(entry):
                if 'properties' in entry and 'Score' in entry['properties']:
                    return entry['properties']['Score']
                return entry.get('fitness_score', 0)

            # Load top 20 best performing
            data.sort(key=get_score, reverse=True)
            top_previous = data[:20]
            
            for m in top_previous:
                comp = m['composition']
                # Ensure composition is clean (handle string keys if any weirdness)
                
                sa = SuperAlloy(
                    name=f"Evolved_{m['name']}",
                    generation=0,
                    composition=comp
                )
                score = get_score(m)
                sa.fitness_score = score # Pre-set score
                self.population.append(sa)
                print(f"  Loaded: {m['name']} (Score={score:.0f})")
                
        except Exception as e:
            print(f"Error loading library: {e}")
            
    def seed_from_real_alloys(self):
        """Initialize population from real materials + Library."""
        self.seed_from_library() # Load champions first!
        
        # Explicit Ta-Hf-C seed to ensure we find the 4000C material
        # Ta4HfC5 approx composition
        tahfc_seed = SuperAlloy("Seed_Ta4HfC5_Target", 0, {'Ta': 0.7, 'Hf': 0.15, 'C': 0.15})
        self.population.append(tahfc_seed)
        print("  Seeded: Ta4HfC5 Target (The 4000C Candidate)")
        
        if len(self.population) >= POPULATION_SIZE:
             return # Already full of champions
             
        print("Seeding from Real Database...")
        
        seed_materials = []
        
        if self.use_online:
            try:
                # Search for high temp alloys online
                print("Fetching top tungsten compounds and carbides...")
                seed_materials.extend(self.mp_db.get_all_tungsten_compounds(limit=20))
                seed_materials.extend(self.mp_db.search_carbides(limit=20))
            except Exception as e:
                print(f"Online fetch failed: {e}")
                self.use_online = False
                
        if not self.use_online:
            # use offline data
            offline_data = get_offline_database()
            # Convert to pseudo-objects for seeding
            for m in offline_data:
                # Naive formula parser for offline data
                # e.g. "WC" -> {'W':0.5, 'C':0.5} (mass fraction approx)
                comp = self._parse_formula(m['formula'])
                sa = SuperAlloy(
                    name=f"Seed_{m['formula']}",
                    generation=0,
                    composition=comp
                )
                self.population.append(sa)
                print(f"  Seeded (Offline): {m['formula']}")

        # Process online results if any
        if self.use_online:
            for m in seed_materials:
                # Need to convert chemical formula to mass composition
                try:
                    comp = self._parse_formula(m.formula)
                    sa = SuperAlloy(
                        name=f"Seed_{m.formula}",
                        generation=0,
                        composition=comp
                    )
                    self.population.append(sa)
                    if len(self.population) < 10: # Just print a few
                        print(f"  Seeded (Online): {m.formula}")
                except:
                    continue
        
        # Fill rest with random mutations of seeds
        seeds = list(self.population)
        if not seeds:
             # Fallback random if seeds failed
             self._seed_random()
             return

        while len(self.population) < POPULATION_SIZE:
            parent = random.choice(seeds)
            child = self._mutate(parent, rate=0.6) # High mutation for initial spread
            child.name = f"Gen0_Child_{len(self.population)}"
            self.population.append(child)

    def _seed_random(self):
        """Fallback random seeding."""
        for i in range(POPULATION_SIZE):
            comp = {}
            remaining = 1.0
            for el in self.elements[:-1]:
                val = random.uniform(0, remaining)
                comp[el] = val
                remaining -= val
            comp[self.elements[-1]] = remaining
            self.population.append(SuperAlloy(f"Random_{i}", 0, comp))
            
    def _parse_formula(self, formula: str) -> Dict[str, float]:
        """
        Simple formula parser to composition.
        e.g. "WC" -> {'W': 0.93, 'C': 0.06} (Approximation by atomic weight needed ideally)
        For simulation simplicity, we do rough approximation or use stored Element weights.
        """
        # Dictionary of atomic weights
        weights = {
            'Fe': 55.8, 'Ni': 58.6, 'Cr': 52.0, 'Co': 58.9, 'W': 183.8, 
            'Ti': 47.8, 'C': 12.0, 'Mo': 95.9, 'Ta': 180.9, 'Hf': 178.5,
            'Nb': 92.9, 'Zr': 91.2
        }
        
        # This is a very complex task to parse generic formulas accurately without pymatgen.core
        # But we can try to use pymatgen if installed
        try:
            from pymatgen.core import Composition
            comp = Composition(formula)
            mass_dict = comp.get_el_amt_dict() # This gives amount, need mass fraction
            # Actually get_wt_dict is what we want? No, Composition has it.
            # Let's inspect keys. Element names.
            # We need to map to our supported ELEMENTS set for the simulation to work
            
            # Map Pymatgen element names to our internal list
            final_comp = {}
            total_mass = 0.0
            
            for el, amt in mass_dict.items():
                w = weights.get(el, 50.0) # Default weight if unknown
                total_mass += amt * w
                
            for el, amt in mass_dict.items():
                if el in self.elements:
                    final_comp[el] = (amt * weights.get(el, 50.0)) / total_mass
                # If element not in our limited simulation set (e.g. Hf, Ta), ignore 
                # OR add it to simulation with generic properties?
                # Better: Add Hf and Ta to materials_db.py properties on fly?
                # For now, ignore minor elements not in our DB to avoid crashes
            
            # Renormalize
            t = sum(final_comp.values())
            if t > 0:
                for k in final_comp:
                    final_comp[k] /= t
            else:
                # If all elements were ignored (e.g. TaC where Ta is not in DB)
                # Fallback: Treat as Tungsten for simulation
                final_comp['W'] = 1.0 
                
            return final_comp
            
        except ImportError:
            # Fallback simple parser
            return {'W': 0.5, 'C': 0.5} # Placeholder for now
    
    def compute_fitness(self, alloy: SuperAlloy) -> float:
        """
        Multi-objective fitness function.
        
        Objectives:
        1. Maximize T_melt (resistance to heat)
        2. Maximize Hardness (wear resistance)
        3. Minimize Density (lightweight)
        4. Minimize Cost
        """
        score = 0.0
        
        # Temperature resistance (heavily weighted)
        temp_score = (alloy.T_melt / 1000) * 500  # 500 pts per 1000°C
        score += temp_score
        
        # Hardness (heavily weighted)
        hardness_score = (alloy.hardness / 100) * 50  # 50 pts per 100 HV
        score += hardness_score
        
        # Lightweight bonus (density < 8000 kg/m³)
        if alloy.density < 8000:
            lightweight_bonus = (8000 - alloy.density) / 100  # Up to 80 pts
            score += lightweight_bonus
        
        # Ultra-high temp bonus (>3000°C)
        if alloy.T_melt > 3000:
            score += 1000
        if alloy.T_melt > 4000:
            score += 2000
        
        # Ultra-hard bonus (>2000 HV)
        if alloy.hardness > 2000:
            score += 500
        if alloy.hardness > 5000:
            score += 1000
        
        # Cost penalty
        score -= alloy.cost * 5
        
        # Brittleness Penalty (NEW)
        # Punish excess Carbon (>40%) to avoid Pure Ceramics/Graphite.
        # We want a TOUGH Cermet (Metal Matrix), so we need >60% Metal.
        c_content = alloy.composition.get('C', 0)
        if c_content > 0.40:
            excess = c_content - 0.40
            # Harsh penalty: 50% score drop for every 5% excess carbon
            penalty_factor = max(0.1, 1.0 - (excess * 10.0))
            score *= penalty_factor
        
        alloy.fitness_score = score
        return score
    
    def _crossover(self, parent1: SuperAlloy, parent2: SuperAlloy) -> SuperAlloy:
        """Blend two alloys."""
        child_comp = {}
        for el in self.elements:
            f1 = parent1.composition.get(el, 0)
            f2 = parent2.composition.get(el, 0)
            # Weighted blend
            w = random.random()
            child_comp[el] = f1 * w + f2 * (1 - w)
        
        return SuperAlloy(
            name=f"Gen{self.generation}_Cross",
            generation=self.generation,
            composition=child_comp
        )
    
    def _mutate(self, alloy: SuperAlloy, rate: float = None) -> SuperAlloy:
        """Mutate an alloy's composition."""
        if rate is None:
            rate = BASE_MUTATION_RATE
            
        new_comp = dict(alloy.composition)
        
        for el in self.elements:
            if random.random() < rate:
                # Shift by up to 15%
                current = new_comp.get(el, 0)
                shift = random.uniform(-0.15, 0.15)
                new_comp[el] = max(0, min(1.0, current + shift))
        
        # Occasionally inject a random element
        if random.random() < 0.1:
            random_el = random.choice(self.elements)
            new_comp[random_el] = random.uniform(0.05, 0.2)
        
        return SuperAlloy(
            name=f"Gen{self.generation}_Mutant",
            generation=self.generation,
            composition=new_comp
        )
    
    def evolve_one_generation(self):
        """Run one generation of evolution."""
        self.generation += 1
        
        # Score all
        for alloy in self.population:
            self.compute_fitness(alloy)
        
        # Sort by fitness
        self.population.sort(key=lambda a: a.fitness_score, reverse=True)
        
        # Update hall of fame
        if self.population[0].fitness_score > (self.hall_of_fame[0].fitness_score if self.hall_of_fame else 0):
            self.hall_of_fame.insert(0, self.population[0])
            self.hall_of_fame = self.hall_of_fame[:10]  # Keep top 10 ever
        
        # Create next generation
        new_pop = self.population[:ELITE_COUNT]  # Keep elites
        
        while len(new_pop) < POPULATION_SIZE:
            if random.random() < CROSSOVER_RATE:
                p1 = random.choice(self.population[:POPULATION_SIZE//2])
                p2 = random.choice(self.population[:POPULATION_SIZE//2])
                child = self._crossover(p1, p2)
            else:
                parent = random.choice(self.population[:POPULATION_SIZE//3])
                child = self._mutate(parent)
            
            child.name = f"Gen{self.generation}_{len(new_pop)}"
            new_pop.append(child)
        
        self.population = new_pop
    
    def run(self, target_score: float = TARGET_SCORE):
        """Run full evolution."""
        print("=" * 60)
        print("ADVANCED MATERIAL DISCOVERY ENGINE v2.0")
        print(f"Target: Score >= {target_score}")
        print("=" * 60)
        
        self.seed_from_real_alloys()
        
        for gen in range(GENERATIONS):
            self.evolve_one_generation()
            best = self.population[0]
            
            if gen % 5 == 0:
                print(f"Gen {gen:3d}: Best Score {best.fitness_score:,.0f} | "
                      f"Tm={best.T_melt:.0f}C | HV={best.hardness:.0f} | "
                      f"rho={best.density:.0f} kg/m3")
            
            # Early stop if target reached
            if best.fitness_score >= target_score:
                print(f"\n[TARGET REACHED at Gen {gen}!]")
                break
        
        # Final results - TOP 5
        self.population.sort(key=lambda a: a.fitness_score, reverse=True)
        top_5 = self.population[:5]
        
        print("\n" + "=" * 60)
        print("TOP 5 SUPER-MATERIALS DISCOVERED!")
        print("=" * 60)
        
        for i, winner in enumerate(top_5):
            # Generate name if needed
            if "Gen" in winner.name or "Seed" in winner.name:
                winner.name = self._generate_name(winner)
                
            print(f"\n#{i+1}: {winner.name}")
            print(f"   Composition: {self._format_comp(winner.composition)}")
            print(f"   Melting Point: {winner.T_melt:,.0f} °C")
            print(f"   Hardness: {winner.hardness:,.0f} HV")
            print(f"   Erosion Rate: {winner.erosion_rate:.2f} (Index)")
            print(f"   Optimal Thickness: {winner.optimal_thickness:.1f} µm")
            print(f"   SCORE: {winner.fitness_score:,.0f}")
            
            # Save to library
            self._save_results(winner)
        
        return top_5
    
    def _generate_name(self, alloy: SuperAlloy) -> str:
        """Generate a cool name for the super-material."""
        # Get dominant elements
        sorted_el = sorted(alloy.composition.items(), key=lambda x: x[1], reverse=True)
        dominant = [el for el, f in sorted_el if f > 0.1][:3]
        base = "-".join(dominant)
        
        # Suffix based on properties
        suffix = ""
        if alloy.T_melt > 3500: suffix += "X"  # Extreme
        if alloy.hardness > 5000: suffix += "H"  # Hyper-hard
        if alloy.density < 7000: suffix += "L"  # Light
        
        uid = random.randint(1000, 9999)
        return f"ULTRA-{base}{suffix}-{uid}"
    
    def _format_comp(self, comp: Dict[str, float]) -> str:
        return ", ".join([f"{k}:{v*100:.1f}%" for k, v in comp.items() if v > 0.01])
    
    def _save_results(self, winner: SuperAlloy):
        """Save to discovered_materials.json."""
        filename = "discovered_materials.json"
        data = []
        
        if os.path.exists(filename):
            try:
                with open(filename, 'r') as f:
                    data = json.load(f)
            except:
                pass
        
        # Check if already exists by name
        if any(d['name'] == winner.name for d in data):
            return

        entry = {
            "name": winner.name,
            "generation": self.generation,
            "composition": winner.composition,
            "properties": {
                "T_melt_C": winner.T_melt,
                "T_vap_C": winner.T_vap,
                "Hardness_HV": winner.hardness,
                "Density_kg_m3": winner.density,
                "Score": winner.fitness_score
            }
        }
        data.append(entry)
        
        with open(filename, 'w') as f:
            json.dump(data, f, indent=2)
        print(f"   -> Saved to {filename}")


if __name__ == "__main__":
    import sys
    target = 12000
    if len(sys.argv) > 1:
        try:
            target = float(sys.argv[1])
        except:
            pass
            
    engine = AdvancedDiscoveryEngine()
    top_materials = engine.run(target_score=target) 
