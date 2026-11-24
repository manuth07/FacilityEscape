# Facility Escape presentation

English desktop FPS, 1920 x 1080 reference layout with Canvas scaling. Preserve the existing industrial environment and keep the gameplay view clear.

- Colors: navy panels #06101A, white text #FFFFFF, cyan actions #3BE6FF, muted labels #A6BFCC, amber warnings #FFA64D, green success #4DFF9E.
- Typography: existing LegacyRuntime font; bold display headings, regular transmission/body text, compact uppercase status labels. No new font dependency.
- Menus share one centered command-panel layout, button treatment and controls reference. MissionFlowView owns non-gameplay screens; GameHud remains the sole owner of gameplay health, cells and device counters.
- Explicit state controls input, cursor, gameplay time and audio. Restart reloads the scene; victory and defeat are exclusive. Extraction uses an unscaled five-second progress/fade sequence.
- Keyboard/mouse UI navigation uses the Input System event module. Preserve readable text at alternate aspect ratios; avoid text over buttons and duplicate HUD values.
