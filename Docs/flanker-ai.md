\# Flanker AI (Systems Engineer, Intelligent Systems)



\## Summary

The Flanker is a utility-AI enemy. When alerted it scores candidate positions around the player and

walks to the best one, re-planning as the player moves or the level changes (for example a crate is

pushed into its route). Once in position it holds, faces the player and shoots, then flanks again from

a different side.



\## Architecture

Utility AI on top of Unity NavMesh pathfinding. A\* (the NavMesh path) is used only as one input

(route length); the decision itself is a weighted score over several terms.



score = (wRoute\*route + wVisibility\*visibility + wExposure\*exposure + wFlank\*flank) / sum(weights)



Every term is normalised to 0..1 (higher is better), so the score is also in 0..1:

\- route: 1 - clamp(routeLength / maxRouteLength). Shorter NavMesh route is better.

\- visibility: 1 if the position has a clear line of sight to the player, else 0.

\- exposure: 1 - fraction of sampled route points that lie inside the player's view cone and in sight.

\- flank: angle between the Flanker's current side and the candidate, divided by 180.



Default weights: route 0.30, visibility 0.30, exposure 0.25, flank 0.15. maxRouteLength = 45 m.



\### Why these weights

\- Route 0.30: arriving late makes the position useless.

\- Visibility 0.30: a position with no line to the player cannot engage.

\- Exposure 0.25: this is what makes it a flanker; it prefers routes the player cannot see.

\- Flank 0.15: a tie-breaker that pushes it to a different side from the direct approach.



\### Candidate generation

Two rings around the player (radius 5 m and 8 m), 16 angles each. Each point is snapped to the NavMesh

(NavMesh.SamplePosition) and discarded if it is closer than 3 m to the player or has no complete path.

The current target is re-scored too, and a new target must beat it by a margin of 0.08 (hysteresis)

so the Flanker does not flip-flop.



\## States

IDLE -> FLANK (score and walk) -> ENGAGE (face the player, hold 2.5 s, shoot) -> FLANK again

(preferring the other side) -> RETURN (alert expired) -> IDLE.



Alert sources:

\- Sight: the player is within 18 m with a clear line.

\- Hearing: a SoundEvent with priority >= 5 within its hearing radius (gunfire is priority 10).

\- Damage: being hit by the player.



The alert lasts 12 s after the last stimulus. Re-planning runs once per second, and immediately if the

route is cut (path status no longer complete).



\## Reaction to the player's weapon

\- The Flanker reacts to gunfire (priority 10) but ignores the noise device (priority 1), so the player's

&#x20; decoy fools the Hunter but not the Flanker.

\- When it takes damage (Health.CurrentHealth drops), FlankerDamageReaction calls FlankerAI.NotifyDamaged().

\- It then re-plans immediately on a different side and, for 6 s, doubles the exposure weight so it favours

&#x20; routes outside the player's view and line of fire.

\- Health has no damage event, so FlankerDamageReaction compares CurrentHealth each frame. This keeps the AI

&#x20; independent of how combat is implemented.



\## Attack

FlankerAttack (a separate component from FlankerAI) fires only while the Flanker is in ENGAGE, within 14 m,

with a clear line of sight to the player.

\- Telegraph: a 0.6 s red aim line before every shot, so the player can take cover.

\- Accuracy falls linearly with distance: 0.85 up close, 0.35 at maximum range (14 m).

\- Damage 10, cooldown 1.5 s.

\- A hit calls Health.TakeDamage on the player; a miss lands up to 1.2 m off target.

\- Each shot emits a SoundEvent at priority 4 (radius 12). The Flanker ignores it (its threshold is 5),

&#x20; but the Hunter may investigate.

\- Movement decisions (FlankerAI) and combat (FlankerAttack) are separate so each can change independently.



\## Files

Game/Assets/\_Project/Scripts/AI/Flanker/

\- FlankWeights.cs: tunable weights

\- FlankCandidate.cs: data for one scored position

\- FlankScoring.cs: pure scoring maths, no scene access

\- FlankerAI.cs: senses, state machine, candidate generation, movement, debug drawing

\- FlankerDamageReaction.cs: reads Health and notifies the AI

\- FlankerAttack.cs: telegraphed ranged attack



Depends on the shared FacilityEscape.AI.Common.SoundEvents and FacilityEscape.Combat.Health.



\## Debugging aids

\- Overlay (top right of the Game view): state, alert time left, target score and the four terms.

\- Scene view gizmos: every candidate coloured red (worst) to green (best), cyan ring on the chosen one.

\- Console: every state change with its reason, every decision with route length, line of sight, exposure

&#x20; and flank angle, and every shot with its accuracy and distance.



\## Optimisation

\- Candidate scoring is spread over several frames (4 candidates per frame) as a coroutine.

\- NavMeshPath.GetCornersNonAlloc with a reusable buffer avoids per-frame garbage.

\- In the exposure test, cheap distance and angle checks run before the raycast.

\- Re-evaluation runs once per second, not every frame; sensing runs every 0.2 s.

\- The Flanker has a kinematic Rigidbody and a single capsule collider on the root object.



\## Testing evidence

Observed in the Play-mode Console log:

\- Sight alert: "Flanker -> FLANK (saw the player)" followed by "Flanker chose ..." with score, route length,

&#x20; line of sight, exposure and flank angle (scores 0.81 to 0.98 across the run, exposure mostly 0.00).

\- Re-planning as the player moves: a first choice with a 18.6 m route was replaced about 3 s later by a

&#x20; 3.2 m route with exposure 0.00.

\- Arrival: "Flanker -> ENGAGE", then a shot: "Flanker fired: HIT | accuracy 0.69, distance 4.5 m"

&#x20; (accuracy matches lerp(0.85, 0.35, 4.5 / 14)).

\- Damage reaction: "Flanker remaining health: 75/100", then "Flanker hit: switching side and favouring

&#x20; cover", then "Flanker -> FLANK (took damage)", then a new "Flanker chose ..." at a different position.

\- Death after four 25-damage hits ("Flanker destroyed"), and the AI stops without errors.

\- The Player's Health decreases on hits.



Further tests (cover blocks the attack, range limit, gunfire alert from cover, decoy ignored, blocked

route re-plan, giving up and returning, coexistence with the Hunter) are listed in docs/test-results.md.



\## Known limitations

\- While alerted it uses the player's true position, like a squad sharing information. Only the Hunter is

&#x20; meant to work from uncertain information.

\- Visibility and exposure are sampled (raycasts and route samples), not exact.

\- In a small room every candidate is close, so the route term is high everywhere and the flank term has little

&#x20; influence. Raising the flank weight, or the damage reaction's boost, makes it switch sides more clearly.

\- The attack needs a Health component on the player, and the shared Health deactivates the object at 0.

