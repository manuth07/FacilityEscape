# Repairer implementation and viva guide

## How the systems connect

| Code | Responsibility |
| --- | --- |
| `RepairableAlly` | Registers candidates while enabled. The Repairer can check eligible allies without scanning the scene every decision. |
| `RepairerAI.Decide` | Reconsiders repair, escape, return and hold every 0.15 seconds. Blocked movement can delay the next attempt. |
| `RepairerAI.FindRepairTarget` and `RepairerScoring.Repair` | Check eligibility and a complete path into repair range, then score missing health and travel distance. The current ally stays selected unless a challenger beats it by the switch margin or the current ally becomes ineligible. |
| `RepairerAI.Hear` and `FindRetreat` | Remember the latest audible gunfire, then compare reachable refuges by separation from the source, travel and cover. Hold when there is a threat and no suitable refuge. |
| `NavigationProgress` | Detects stalled movement. The Repairer clears the path and briefly backs off a blocked ally before trying again. |
| `Health.Heal` | Caps each healing tick at maximum health. The Repairer checks range and line of sight before healing. |
| `RepairerRepairBeam` | Rotates the rigid shoulder mesh toward its work pose and draws a mint line from the emitter to the ally. The line turns off when active repair ends. A small idle motion fades in while holding without a threat. |
| `PowerCell`, `PowerCellManager`, `ExitTerminal` | A trigger resolves the player's `CharacterController`. The manager accepts each configured cell once. At three cells the terminal allows extraction if the player faces it from within range and presses E. |

The Repairer uses a baked Unity NavMesh and obstacle carving. It uses authored refuge markers and remembers one gunfire source at a time. Its simple shoulder animation follows the existing FBX hierarchy and needs no Animator controller. The factory and demo share the Repairer prefab. The custom Power Cell is an exhibit in the demo, while the factory pickups have their own visuals.

## Demonstration and verification

1. In Unity 6000.6.2f1, open `Assets/_Project/Scenes/RepairerDemo.unity` and enter Play Mode. Wait briefly to show the arm at rest. Press `1` for an injured ally. Use `WASD`, the mouse and left click to move, aim and fire nearby. Watch the beam disappear, the threat timer count down, and the Repairer finish healing and return home.
2. Press `F2` for competing allies and `3` to damage the maintenance drone. Watch the selected ally and its score. Press `F3`, then `B` to open the barrier. Press `R` to reset the scenario. Press `L` to compare Power Cell lighting. `F1` returns to the single-ally setup and `2` toggles the Sentinel's disconnected island.
3. Exit Play Mode. Choose **Tools → Repairer → Run showcase checks**. Read the Console for the suite result and any separate errors.
4. Open `Assets/_Project/Scenes/FacilityEscape_Main.unity`. Choose **Start Mission** and **Begin Mission**. Walk into A, B and C and check the HUD increments by one each time. Revisit each pickup position to check it does not increment again. Check the terminal remains locked up to `2/3`. After `3/3`, approach within interaction range, look at the terminal and press `E`. Confirm the extraction countdown completes and victory appears.
5. Build Windows Intel 64-bit using the configured factory scene. Open the output and check the main menu and mission start. Record Console errors, missing references and build results separately from the showcase assertions.

### Manual input and layout check

Use the physical keyboard and mouse with the Game tab focused. Click inside the Game view to capture input. A click during active gameplay can fire the PulseGun, so aim at the floor and let the threat timer clear before checking idle motion. Use a Game view scale that fits the viewport. Test Free Aspect and 16:9, then narrow the Game view to check the overflow. In the showcase, press `Esc` to release the pointer and scroll the diagnostic panel if needed. Check each scenario's instructions and the last line of the control legend. In F3, open the barrier, then press `R` and confirm the barrier starts closed again. Toggle `L` twice to compare and restore lighting.

### Factory walking route

The following landmarks and coordinates come from the scene. The route still needs a physical Play Mode run. Positions use Unity world coordinates. The player starts at `(0, 0.02, 0)`. Face the entry access door, approach it and press `E`, then follow the passage roughly along negative X into the Main Factory. Stay near `z = 0` until the branch toward each cell.

| Leg | Route and expected result |
| --- | --- |
| A | From the main floor, take the positive-Z branch near `x = -16.5`, through the doorway around `z = 8.25`. Walk into the cell in the Cooling Systems room at `(-16.5, 0.6, 12.5)`. Check `1/3`, then step through the spot again and check it stays at `1/3`. |
| B | Return to the same point on the main floor and cross to the negative-Z branch near `x = -16.5`, through the doorway around `z = -8.25`. Walk into the Power Systems cell at `(-16.5, 0.6, -12.5)`. Check `2/3` and confirm extraction is still locked. |
| C | Return to the main floor and head toward the Security Lab along negative X, through the doorway near `(-26.75, 0, 0)`. Approach the cell at `(-32, 0.6, 0)` and check `3/3`. |
| Extraction | Continue along negative X through the exit connection near `(-37.25, 0, 0)` and follow **Emergency Extraction**. Face the terminal at `(-44.9, 1.65, 0)` from within 2.5 m. Press `E` and check for the five-second extraction sequence followed by victory. |

The three pickup objects have enabled sphere triggers with a serialized radius of 0.75 m. Walk through the model center with the player controller to exercise the trigger. Record the HUD and terminal state at each stage and use `E` at access doors as needed.

## Two-minute viva

**0:00–0:30.** “My Repairer compares four actions. I damage the Sentinel so repair becomes worthwhile. It checks the path, range and sight before healing. The beam starts at the tool tip, which moves with the shoulder.”

**0:30–1:00.** “Nearby gunfire changes the priorities. The robot compares cover, distance from the shot and travel cost. Watch the beam stop and the threat timer fall. The Repairer can then finish healing and return home.”

**1:00–1:30.** “In the competing-ally scene, a small score change keeps the current patient. Damaging the drone enough can make it the priority. That margin is hysteresis.”

**1:30–2:00.** “The barrier makes a complete path impossible. Opening it lets the robot retry. I also made the separate Repairer body and shoulder mesh and the Power Cell casing and core in Blender. This exhibit lets me show the Power Cell under two lighting conditions.”

## Likely questions

- **Why use utility scores?** The Repairer can compare patient urgency, route cost and safety using simple numbers, and the overlay makes the choice visible.
- **Why keep the current ally?** Slight differences in distance can change scores frequently. Hysteresis prevents unnecessary switching.
- **How do you recover from a closed door?** Unity carves the route while the door is closed. The Repairer rejects incomplete paths and reevaluates after the door opens.
- **How do you prevent duplicate pickups?** The pickup deactivates after collection, and the manager's set can register a configured cell only once.
- **What would you measure before optimizing?** NavMesh query time with multiple injured allies and changing obstacles, along with how often movement stalls and causes a retry.
