# Facility Escape next phase

## Baseline and priorities

Fetched `origin`. Local `dev` matches `925bbcf`, the PR #22 merge containing all ten Repairer upgrade commits. The clean checkout was switched to `codex/repairer-next-phase` from that commit. The two stashes and other worktrees were retained.

1. Verify mission pickups, extraction, the Repairer suite, and the configured Windows build in Unity.
2. Improve Repairer idle presentation and showcase guidance using the existing arm and demo scripts.
3. Correct model usage documentation and prepare a repeatable viva demonstration.
4. Revisit AI, environment and audio only where playtesting identifies a problem.

## Findings and verification

- `FacilityEscape_Main` is the enabled build scene. `RepairerDemo` runs separately in Editor Play Mode.
- Both scenes reference the custom Repairer prefab. `RepairerDemo` references the custom Power Cell FBX. The factory pickups use separate scene-authored casing and contact visuals.
- The existing factory flow uses a trigger and a registered set of Power Cells, with extraction gated at three. Pickup sound already exists through `PowerCellFeedback` and `Sfx`.
- The Repairer already uses periodic scoring, reused path and raycast buffers, hysteresis, navigation progress checks, and balanced sound subscriptions. Its threat memory tracks the latest audible gunfire event. A second beam implementation (`RepairerFeedback`) has no references in the checked scenes or prefabs and is left available for existing users.
- The prior session's QA logs report passing gameplay assertions and pickup registration through direct `TryCollect` calls. The fresh Play Mode and Windows batch build attempts exited during project startup while Unity 6000.6.2f1 already had this project open. Neither compilation nor build completed in those attempts.

## Work and remaining verification

- Added subdued tool-arm idle movement, scenario-specific demo instructions, and a Repairer viva and verification guide. The factory scene, player controls, AI decisions and audio assets are preserved.
- Run the showcase suite, check the idle transition and overlay in the Game view, inspect both scenes for missing references, and walk through all three factory pickup triggers. Confirm extraction stays locked until the third pickup, then activate the terminal with E and observe victory.
- Run the Windows build in the current Unity instance or with that instance closed, and record the build outcome and Console diagnostics. Profile pathfinding under multiple allies before adjusting the decision interval or adding caching.

## Showcase failure investigation

| Item | Status | Evidence |
| --- | --- | --- |
| Previous overlay at the observed Game view size | FAIL | The fixed-height panel clipped the last control line once the scenario instruction wrapped. The panel now uses two score rows, wrapped labels and a scroll view for overflow. |
| Revised overlay at Free Aspect and 16:9 | NOT TESTED | Unity rebuilt the assemblies. The Windows automation helper no longer exposed the running Editor window after recompilation. |
| Original suite run 1 | FAIL | `initial gunfire expires` timed out while the Console showed repeated shots. `PulseGun.Update` fires while the left button is held and the focused Game view has a locked cursor. Each audible shot refreshes the Repairer's timer. |
| Original suite run 2 | FAIL | The combined `TryFire` and HP assertion could not distinguish a rejected shot from a miss. Live input could fire first, start the cooldown, or move the player and camera. The precise reason for this run is unconfirmed. |
| Revised suite | NOT TESTED | `RepairerPlayChecks.cs` temporarily mutes live keyboard and mouse input, aims with the gun's configured camera, checks one accepted shot separately from HP reduction, and flags further shots during expiry. The gun, damage rules and AI are unchanged. |
| Manual presentation controls and animation | NOT TESTED | Follow the physical keyboard and focus procedure in the viva guide. The prior run only showed the Single opening overlay and a threat response. |
| Factory menu and initial HUD | PASS | Prior interactive run reached Start Mission, Begin Mission and `0/3`. |
| Actual factory triggers, extraction and victory | NOT TESTED | The verified serialized positions, trigger radius, door landmarks and manual route are in the viva guide. |
| Windows build and complete asset audit | NOT TESTED | The build remains gated on the gameplay checks. `FacilityEscape_Main` is the only enabled build scene. |

Changed files in this investigation are `RepairerDemo.cs`, `RepairerPlayChecks.cs`, `Docs/repairer-viva.md` and this progress log. No scene, prefab, model, material or Unity metadata was edited. No additional Unity instance was started.
