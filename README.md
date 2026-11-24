# Facility Escape: Emergency Protocol

A small indoor science-fiction FPS. A research facility's extraction network has failed, and malfunctioning security robots treat the player as an intruder. Recover three backup power cells, restore the extraction terminal and escape.

Our Unity group project is in `Game/` (relative to the repository root).

- Required Unity Editor version for all members: **6000.6.2f1**.
- Blender: **4.5.14 LTS**.

Teammates should clone this repository, then use Unity Hub to add and open the `Game/` project with Unity Editor **6000.6.2f1**.

`Game/Assets/_Project/` contains our own assets, organized into Scenes, Scripts, Prefabs, Models, Materials, Audio, and UI. `Game/Assets/ThirdParty/` contains downloaded assets. Keep original Blender work in `ArtSource/Blender/` and project documentation in `Docs/`.

Each shared scene or Blender model should have one editor at a time; coordinate with the team before editing.

After creating folders on disk, return to Unity and let it import them and generate their folder `.meta` files. Include those Unity-generated files in version control alongside assets; do not create GUIDs or `.meta` files manually.

## Play and controls

Open `Game/Assets/_Project/Scenes/FacilityEscape_Main.unity`, enter Play Mode, choose **Start Mission**, then **Begin Mission**. The menu/briefing suspend gameplay. Collect one cell in each of Cell Areas A, B and C; all three unlock the exit connection and terminal. Face the terminal within 2.5 m and press E to begin the five-second extraction sequence. Victory/defeat offer restart and main menu; restarting reloads the scene and resets mission objects.

| Input | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Left mouse | Fire Pulse Gun |
| E | Interact with doors and extraction terminal |
| G | Throw a noise device (five per mission) |
| Escape | Pause / resume |
| Walk into item | Collect a power cell or health supply |
| Walk into movable crate | Push it using Rigidbody physics |

## AI and shared systems

| Agent | Actual architecture | Role |
| --- | --- | --- |
| Hunter | Explicit FSM with visual perception, last-known-position/search states and weighted sound-stimulus scoring | Personnel tracking |
| Flanker | FSM with weighted tactical utility scoring over sampled NavMesh candidate positions | Tactical response and flanking |
| Sentinel | Behaviour-tree-style priority selector implemented in C# (not a separate BT framework) | Restricted-area defense, cover and ranged attacks |
| Repairer | Utility AI scoring repair, escape, return and hold actions | Repair eligible allies and avoid threats |

All agents use **Unity NavMesh** and the existing `FacilityEscape_Robot` agent type. There is no custom A* implementation. A baked NavMesh supplies navigation; doors and movable crates use dynamic obstacle carving. Shared `Health`, camera-based PulseGun hitscan and classified `SoundEvents` support combat and perception. GameFlowController coordinates menus, input, pause, extraction and results; GameHud reads existing health, cell and device state. Enemy AI algorithms remain separate from presentation and mission flow.

## Contributions

Attribution below follows repository commit authorship and existing project documentation; it does not assert unrecorded work or model ownership.

| Contributor | Original work and subsequent integration |
| --- | --- |
| Manuth Wilegoda / Manuth | Hunter and core facility/player/combat/objective work; subsequent navigation repair, Hunter combat and Repairer integration, reliability/balance work, primitive-based visual pass, unified game flow, extraction and release integration |
| Oshadi Thilakarathne | Flanker tactical AI/combat, interaction systems (doors, crates and noise devices), HUD, gun feedback and audio infrastructure |
| Dinil Dilmith Hettiarachchi | Sentinel priority-selector AI, defensive navigation/cover, combat, motion/feedback and related documentation |
| Kaveesha Gunarathne | Repairer utility scoring/behaviour, repairable ally integration and capped healing support |

Later integration changes extend teammates' original systems rather than replacing their authorship. See `Docs/flanker-ai.md`, `Docs/sentinel-ai.md`, `Docs/interaction-system.md`, `Docs/feedback-ui-audio.md` and `Docs/ai-reliability.md` for implementation context.

## Windows release

Use Unity **6000.6.2f1**, **File → Build Profiles → Windows**, target **Windows Intel 64-bit**, with Development Build disabled. Include only `Assets/_Project/Scenes/FacilityEscape_Main.unity`; it starts at the main menu. Use Mono as configured by the project. Build to a fresh directory such as `Game/Builds/WindowsViva_20261125/FacilityEscape.exe`, preserving any previous working build until success.

Ship the entire player folder, including the executable, `FacilityEscape_Data`, UnityPlayer.dll and Mono runtime files, rather than the EXE alone. Launch `FacilityEscape.exe`; use Quit Game from the main menu to close the standalone application. Generated builds are Git-ignored. Build results and validation scope are recorded in `Docs/Phase06_Release.md`.

## Known limitations and assessment evidence

- No original `.blend`, Maya source or original imported model pair is present in `ArtSource/Blender` / `_Project/Models`. Barking Dog modular meshes are third-party assets; robot, weapon and battery assemblies use Unity primitives and are not Blender-authored models. The requirement for two original authored models remains outstanding.
- The main scene has an existing baked NavMesh, but no baked lightmaps. Lighting is realtime/ambient. There is no skeletal Animator setup; navigation, turning and some procedural feedback/motion are implemented in scripts. Do not present this as a complete character-animation pipeline.
- CharacterController environment collision, Rigidbody crate pushing and throwable physics are implemented. Visual art generally uses disabled decorative colliders over greybox layout colliders.
- Targeted MCP-assisted checks and a standalone smoke test do not replace a full manual combat/mission playthrough or multi-machine compatibility testing. See the release notes for exactly what was exercised.
