# Phase 03: AI reliability and gameplay fairness

## Scope and ownership

Based on release/viva-final at 9c56b34. The original Hunter FSM, Flanker tactical
scoring, Sentinel priority selector and KaveeshaGunarathne's Repairer utility
formulas remain in place. This phase adds reliability guards, encounter tuning,
finite health recovery and restrained damage feedback. Navigation remains Unity
NavMesh; there is no custom A* implementation.

## Diagnosis and changes

- The unmodified starting room remained at 100 health for approximately 40 seconds.
  Combined offensive pressure after entering the factory, rather than unavoidable
  spawn damage, is the main difficulty concern.
- A runtime probe confirmed that Flanker regarded an inactive player as visible.
  Its perception, sound response and attack now reject inactive/dead targets.
- Sentinel's Drive accepted a partial route behind a closed door. It now validates
  complete paths, rejects stalled progress and retries after a bounded delay.
- NavigationProgress is a passive measurement helper, not a movement controller.
  Hunter patrol skips/retries blocked waypoints; Hunter pursuit/search falls back
  into search; Flanker reevaluates tactical destinations or waits/retries its
  return; Repairer temporarily excludes a stalled target and reevaluates utility.
- A normal-health scripted run found that a fully open door leaf physically
  blocked the player while navigation ignored it. Door retains the previous
  hold-until-fully-open blocker and adds runtime carving for the open physical
  panel. No baked navigation data or room geometry was changed.

## Balance values

| Parameter | Before | After |
| --- | --- | --- |
| Hunter damage / cooldown | 15 / 1.2 s | 10 / 1.5 s |
| Hunter telegraph / range / preferred distance | 0.35 s / 8 m / 5.5 m | unchanged |
| Flanker damage / cooldown | 10 / 1.5 s | 8 / 2 s |
| Flanker telegraph / tactical scoring | 0.6 s / original weights | unchanged |
| Sentinel damage / interval | 8 / 1 s | 8 / 1.5 s |
| Repairer heal / interval | 12 / 0.7 s | 10 / 1 s |
| Repairer health / range / gunfire memory | 80 / 1.8 m / 5 s | unchanged |
| Player max health / gun damage | 100 / 25 | unchanged |
| Damage tint peak / low-health tint | 0.30 / 0.08-0.14 | 0.12 / 0.02-0.04 |

Hunter and Flanker cooldowns precede the following telegraph. Their shortest
sustained firing intervals are therefore approximately 1.85 s and 2.6 s.
Repair decisions occur every 0.15 s, so the 1 s heal interval rounds up to a
decision tick rather than firing faster than configured.

Flanker moved from (-16.5, 0.05, -1.5) to (-23, 0.05, 4), validated on the existing
NavMesh with clear body space. Other spawns, patrol points and the six-room layout
remain unchanged. Sentinel remains assigned to its defensive area.

## Distraction policy

- Hunter investigates meaningful reachable sounds unless visual contact wins.
- Flanker retains priority >= 5 tactical alerts. Ordinary priority-1 device beeps
  do not pull it away from a visible player; gunfire still alerts it.
- Sentinel retains its defensive station rather than roaming after beeps.
- Repairer treats classified gunfire as a threat; ordinary beeps are not gunfire.

PlayerThrower.TryThrow is the same physical throw used by G, with validation
before inventory consumption. A runtime test recorded 5 -> 4 devices, three
priority-1/non-gunfire beeps, Hunter investigation, Flanker idle and Repairer
threat level zero.

## Limited recovery

HealthPickup uses shared Health.Heal, defaults to 30 HP and consumes itself only
after a valid living-player heal. Full-health players, enemies, dead players and
repeat collection are rejected. The prefab has a dark body, small cyan-green
medical cross, trigger collider and kinematic Rigidbody. It uses the existing
pickup chime and logs the actual health restored.

Two instances are placed at (-12, 0.25, -5) in Main Factory and (-28, 0.25, -2)
inside Cell Area C. They add no solid navigation obstruction. Neither is in the
starting room.

## Recorded runtime evidence

These are Unity MCP runtime probes, not a new automated unit-test suite.
Temporary test arrangements and runInBackground were not saved.

| Check | Evidence |
| --- | --- |
| Initialization | Four agents on NavMesh; player starts at 100; no missing scripts |
| Hunter timing | Shot times 5.722 and 7.579; player 100 -> 90 -> 80 |
| Hunter search | Isolated state probe recorded CHASE -> ATTACK -> CHASE -> SEARCH_LAST_KNOWN -> SEARCH_AREA -> PATROL after hiding behind the closed start-room door; attacks were disabled only in this state-isolation probe |
| Flanker | Tactical evaluations retained 26-32 candidates; blocked RETURN waited, then moved when the actual door opened; inactive-player visibility false |
| Sentinel | Shot times 36.184, 37.685, 39.185; player 100 -> 92 -> 84 -> 76; damage provoked Seek cover |
| Repairer | Ally 50 -> 60 -> 70 -> 80 -> 90; recorded intervals about 1.03-1.07 s |
| Repairer threat | Repair 0.400 lost to Escape 0.992; Repair resumed after about 5 s |
| Closed doors | Five room routes partial with doors closed; Sentinel rejected a blocked route; Hunter skipped a blocked waypoint |
| No-progress timeout | With a complete path but zero movement, Hunter advanced its waypoint after 4.023 s (3 s timeout plus 1 s recovery wait) |
| Open doors | Ordinary connections complete; exit complete after 3 cells and opening its door; open Cell C leaf produced a valid detour |
| Movable crate | Moving existing Crate_C changed a 5.00 m route to 5.47 m with four corners |
| Health pickup | 55 -> 85; capped at 100; full/dead/enemy/duplicate attempts rejected |
| Damage tint | Five repeated 5-damage hits sampled at maximum alpha 0.027, fading to zero |
| Mission regression | Actual CharacterController movement collected all 3 cells and activated the terminal |

## Normal-health run and its limits

A fresh scripted run used the existing CharacterController, nearby door
interactions, actual PulseGun.TryFire, physical trigger pickups and power cells,
and ExitTerminal.TryActivate with all four agents active. No player health was
artificially increased. It finished with:

- Starting/max health: 100/100.
- Damage received: 10; minimum health: 90.
- Pickup healing: 10, capped at 100; one pickup consumed.
- Enemies eliminated: 4; power cells: 3; extraction successful.
- Scripted route time: approximately 20.5 seconds.

This precise-aim automated run proves flow and collision/navigation integration,
not human difficulty. It does not establish the intended 3-5 minute duration.
A previous run exposed the open-leaf collision problem and stalled there; the
completed run followed its correction. A stationary exposed player can still
be defeated. Full manual balance acceptance remains outstanding.

## Manual acceptance and Phase 04

1. Play at normal 100 health with ordinary mouse/WASD control; do not use scripted
   aim or Inspector health changes. Time a full successful run.
2. Use cover and G, try both pickup routes, and verify readability during repeated
   incoming damage. Confirm all three cells and E extraction.
3. Close/reopen doors while being pursued, push a crate, and check that enemies
   neither pass through panels nor remain stuck indefinitely.
4. Check each agent's diagnostics and behaviour in isolation if a problem appears.

Mandatory Phase 04 task: replace Repairer's excessively glowing, light-bulb-like
appearance with a readable support-robot presentation while preserving its
original prefab identity and functional components. No robot redesign, dummy
cleanup, menu work, narrative work or release build was done in Phase 03.
