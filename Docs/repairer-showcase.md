# Two-minute Repairer showcase

Open **RepairerDemo** and enter Play Mode. The demo runs independently of the factory mission. The upper-left panel reports action scores, target, health and threat time.

| Key | Demo action |
| --- | --- |
| F1 / F2 / F3 | Start single-ally, competing-allies or blocked-route mode |
| R | Reset the current scenario inside Play Mode |
| 1 | Damage the Sentinel without emitting sound |
| 2 | Move the Sentinel between the main floor and the disconnected island |
| 3 | Damage the competing ally in F2 |
| B | Open or close the full-width maintenance barrier in F3 |
| L | Toggle the exhibit between normal and reduced directional lighting |
| WASD / mouse / left click | Move, look and fire the real PulseGun |

**0:00–0:30.** “This is my utility-based support robot. I press 1 to damage the Sentinel silently. Repair uses missing health and complete path length. Watch the target name, its rising HP, the shoulder rotation and the mint beam. The beam follows the tool tip.”

**0:30–0:55.** “Nearby gunfire changes the decision. I click the floor from near the Repairer. Escape outranks repair. Travel cost, distance from the sound and cover distinguish the reachable refuges. Healing stops during retreat and resumes when the threat timer expires. At full health the beam switches off and the robot returns home.”

**0:55–1:20.** “F2 starts two damaged allies. I press 3 to make the maintenance drone more urgent. A small score advantage preserves the incumbent; a larger advantage changes the selected ally. The score shown belongs to the chosen target.”

**1:20–1:45.** “F3 puts a carving barrier between the robot and its patient. A damaged ally alone does not qualify. Watch the rejection reason, then I press B. Once the NavMesh offers a complete route into repair range, the robot retries.”

**1:45–2:00.** “R resets this scenario. The Power Cell exhibit has separate casing and core meshes. I use L to compare its cyan core and darker ribs under two light levels. The playable factory still owns its original pickups and extraction logic.”

For repeatable checks, leave Play Mode and choose **Tools → Repairer → Run showcase checks**. The Console reports each gameplay assertion. The suite enters Play Mode and exits it when complete.
