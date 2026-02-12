
import subprocess
import time
import json
import os
import sys

TARGET_SCORE = 1e30 # Effectively infinite
SCRIPT_NAME = "advanced_discovery.py"
DATA_FILE = "discovered_materials.json"

def get_best_score():
    if not os.path.exists(DATA_FILE):
        return 0
    try:
        with open(DATA_FILE, 'r') as f:
            data = json.load(f)
        
        best_score = 0
        for entry in data:
            # Handle both formats
            s = 0
            if 'properties' in entry and 'Score' in entry['properties']:
                s = entry['properties']['Score']
            else:
                s = entry.get('fitness_score', 0)
            
            if s > best_score:
                best_score = s
        return best_score
    except Exception as e:
        print(f"Error reading DB: {e}")
        return 0

def main():
    print(f"Starting INFINITE EVOLUTION LOOP")
    print(f"Target Score: {TARGET_SCORE}")
    print("="*60)

    iteration = 1
    while True:
        print(f"\n[Iteration {iteration}] Launching {SCRIPT_NAME}...")
        
        # Run the discovery script
        # We pass the target score so it knows when to stop internally if it hits it
        try:
            # Using python explicitly
            # Run as module from root
            subprocess.run([sys.executable, "-m", "discovery.advanced_discovery", str(TARGET_SCORE)], check=True)
        except subprocess.CalledProcessError as e:
            print(f"Error running script: {e}")
            time.sleep(5)
            continue
        except KeyboardInterrupt:
            print("\nStopped by user.")
            break

        # Check results
        current_best = get_best_score()
        print(f"Current Global Best Score: {current_best:,.0f}")
        
        if current_best >= TARGET_SCORE:
            print("\n" + "="*60)
            print(f"SUCCESS! Target Score {TARGET_SCORE} reached/exceeded!")
            print(f"Final Best Score: {current_best:,.0f}")
            print("="*60)
            break
        
        iteration += 1
        time.sleep(2) # Brief pause

if __name__ == "__main__":
    main()
