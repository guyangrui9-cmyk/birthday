# Gentle walking animation
Use the existing character artwork and outfits. Animate the exposed lower legs with opposite small swings, add a two-pixel body bounce and subtle body sway. Ease movement in and out; collision stops stepping. Match Windows and macOS rendering. Keep artwork unchanged.

Implementation: update player rendering and motion state on both platforms, compile Windows, run the existing movement self-test, and inspect rendered walking poses before pushing.
