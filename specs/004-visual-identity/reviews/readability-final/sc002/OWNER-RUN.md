# SC-002 timed trial: exact steps for the owner (T445)

SC-002 (spec): in 10 randomised 12-slot factory screens you point out the stuck machine within 2 seconds in at least 9 of 10 trials. You know the art, so this is a recognisability check. You run **two** sets: `standard` (running machines move) and `reduced-motion` (running machines are frozen, as with the reduced-motion preference on). Nothing in the folders contains answers.

## Before you start

1. Use Chrome or Edge on a desktop, one mouse, no other windows competing for attention. Browser zoom 100 %.
2. The screens are in-game captures of the shipped build (emulator class, 1080 x 2400, 420 dpi, zoom n = 3, with the real HUD and floor). A "stuck machine" is a machine that shows a **problem cue**: waiting (hourglass), missing input (arrow into a tray) or storage full (stack of bars). Paused and inactive machines are benign distractors; running machines have no cue.
3. Make the browser window phone-shaped: about 450 px wide and 950 px tall (undock the window and drag its edge, or use the developer tools device toolbar at 412 x 915). The page scales the screen to the window height.

## Run (about 3 minutes per set)

1. Open `specs/004-visual-identity/reviews/readability-final/sc002/standard/trial.html` (double-click or drag it into the browser). Do not open any other file in that folder before the run.
2. Press the start button. One unscored practice screen comes first, then ten timed screens. For every screen: press the button, look, and click the stuck machine as fast as you can. The timer starts when the screen appears and stops at your click. Only the first click per screen counts.
3. After the tenth screen the page shows "Done". Press **Save log**; the browser downloads `sc002-log-standard.json`. (The same JSON is in the text box as a fallback.)
4. Move the file to `specs/004-visual-identity/reviews/readability-final/sc002/logs/sc002-log-standard.json`.
5. Take a short break, then repeat with `sc002/reduced-motion/trial.html` and save `sc002-log-reduced-motion.json` to the same `logs/` folder. The two sets use different screens.
6. Hand both logs to the parent. The parent scores them against the keys that are kept outside the repository (`final_readability.py score --key <key> --log <log>`), records the counts, the per-state breakdown and the median time in `readability-final.md`, and decides.

## Notes

- If you click the wrong machine or are slow, carry on; do not repeat a screen. Misses count.
- Say if something distorted the run (a phone call, a window resize); the log records the window size and the display size of the screen.
- Pass rule: at least 9 of 10 correct within 2.0 s in each set; a set that fails is reported as such. The reduced-motion set is the measurement requested by the Art Direction and Game Designer reviews (running = no cue is otherwise only proven with motion).
