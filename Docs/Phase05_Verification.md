# Phase 05: complete game experience

Unity 6000.6.2f1, `FacilityEscape_Main`, `feat/complete-game-experience`.

Implemented scene-local `GameFlowController` with MainMenu, Briefing, Playing, Paused, Extracting, Victory and Defeat. `MissionFlowView` owns the overlay UI; existing `GameHud` owns gameplay counters and feedback. Restart reloads the existing scene, including agents, objectives, pickups and thrown objects. Shared Health retains enemy death behavior; player death keeps the camera active and opens Defeat. The extraction endpoint remains the existing terminal.

## Executed in Unity Play Mode

- Main menu and briefing freeze gameplay, unlock the cursor and reject damage.
- Start/Begin button callbacks enter Briefing/Playing. One EventSystem exists.
- Queued Escape key input pauses and resumes; time scale changes 0/1 and cursor locking is restored.
- Lethal damage enters Defeat while Player and PlayerCamera stay active.
- Retry restores 100 health, five devices, three active cells, four agents and two health pickups.
- Extraction is rejected at 0/3. All three existing cells were collected through `TryCollect` with the configured player controller, without changing manager data.
- Player was temporarily moved to the terminal in Play Mode. The existing proximity/line-of-sight check passed. `TryActivate` entered Extracting; duplicate activation and combat damage were rejected.
- The five-second unscaled sequence reached Victory, with actual 3/3 cells, health, time and enemy count. Victory damage was rejected.
- Victory restart resets progress, and Main Menu reload returns to frozen menu state.
- Console had no errors after the final runtime checks. Edit Mode scan found zero missing scripts; all six GameFlow references are assigned, four agents and three cells remain.

Rendered captures were visually inspected under `Game/Recordings/Phase05/`: 01-main-menu, 04-briefing-final, 05-pause-final, 06-defeat, 07-gameplay-hud, 08-extraction-sequence, 09-victory and 10-controls. Captures remain local and Git-ignored. Earlier numbered captures include discarded setup attempts.

These are targeted MCP-assisted integration checks, not a full manual combat playthrough. Standalone Quit, standalone build behavior, audio listening quality, and alternate-aspect-ratio layouts were not manually tested. Existing AI/navigation algorithms and the NavMesh bake were not changed.

## Manual acceptance route

1. Open FacilityEscape_Main and enter Play Mode. Start Mission, read the transmission, Begin Mission.
2. Use WASD/mouse, left-click to fire, E for doors, G for noise devices. Confirm Escape pauses and resumes.
3. Fight security units and collect the cells in A/B/C; verify health supplies, notifications and cell indicators.
4. Reach the Exit Room, face the terminal within 2.5 m, press E, and watch the complete restoration/fade/Victory sequence.
5. Play Again and verify reset. Allow the player to die, Retry, then return to Main Menu.
6. In a standalone build, verify Controls, Pause and Quit; check the target display resolution and audio volume.
