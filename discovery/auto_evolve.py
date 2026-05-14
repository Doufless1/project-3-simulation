
import logging
import subprocess
import time
import json
import os
import sys

logger = logging.getLogger(__name__)

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
        logger.error("Error reading DB: %s", e)
        return 0

def main():
    logging.basicConfig(level=logging.INFO, format="%(message)s")
    logger.info("Starting INFINITE EVOLUTION LOOP")
    logger.info("Target Score: %s", TARGET_SCORE)
    logger.info("=" * 60)

    iteration = 1
    while True:
        logger.info("\n[Iteration %d] Launching %s...", iteration, SCRIPT_NAME)
        
        # Run the discovery script
        # We pass the target score so it knows when to stop internally if it hits it
        try:
            # Using python explicitly
            # Run as module from root
            subprocess.run([sys.executable, "-m", "discovery.advanced_discovery", str(TARGET_SCORE)], check=True)
        except subprocess.CalledProcessError as e:
            logger.error("Error running script: %s", e)
            time.sleep(5)
            continue
        except KeyboardInterrupt:
            logger.info("\nStopped by user.")
            break

        # Check results
        current_best = get_best_score()
        logger.info("Current Global Best Score: %,.0f", current_best)
        
        if current_best >= TARGET_SCORE:
            logger.info("\n" + "=" * 60)
            logger.info("SUCCESS! Target Score %s reached/exceeded!", TARGET_SCORE)
            logger.info("Final Best Score: %,.0f", current_best)
            logger.info("=" * 60)
            break
        
        iteration += 1
        time.sleep(2) # Brief pause

if __name__ == "__main__":
    main()
