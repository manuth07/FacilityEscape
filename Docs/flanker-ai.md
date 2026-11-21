\# Flanker AI (Systems Engineer, Intelligent Systems)



\## Architecture

Utility AI. When alerted (sight, or gunfire with priority >= 5) the Flanker scores candidate positions

on two rings around the player (5 m and 8 m, 16 angles each) and walks to the best one.



score = (wRoute\*route + wVisibility\*visibility + wExposure\*exposure + wFlank\*flank) / sum(weights)



\- route: 1 - clamp(routeLength / maxRouteLength), NavMesh path length

\- visibility: 1 if the position has a clear line of sight to the player, else 0

\- exposure: 1 - fraction of sampled route points inside the player's view cone and in sight

\- flank: angle between the Flanker's starting side and the candidate, divided by 180



Default weights: route 0.30, visibility 0.30, exposure 0.25, flank 0.15.



\## Behaviour

IDLE -> FLANK (score and walk) -> ENGAGE (face the player, hold) -> FLANK again (other side) -> RETURN when the alert expires.

Re-plans every second. Hysteresis (switch margin 0.08) stops it flip-flopping.

Re-plans immediately if the route is cut (for example a pushed crate).

Ignores the noise device (priority 1), reacts to gunfire (priority 10).



\## Files

Game/Assets/\_Project/Scripts/AI/Flanker/ (FlankWeights, FlankCandidate, FlankScoring, FlankerAI)



\## Optimisation

\- Candidate scoring is spread over frames (4 candidates per frame).

\- NavMeshPath.GetCornersNonAlloc and a reusable buffer avoid garbage.

\- Cheap distance and angle checks run before raycasts in the exposure test.

\- Re-evaluation runs once per second, not every frame.



\## Reaction to the player's weapon

\- The Flanker reacts to gunfire (SoundEvents priority >= 5) but ignores the noise device (priority 1).

\- When it takes damage (Health.CurrentHealth drops), FlankerDamageReaction calls FlankerAI.NotifyDamaged().

\- It then re-plans immediately on a different side and, for 6 s, doubles the exposure weight so it favours routes outside the player's view and line of fire.

\- Health is read by comparing CurrentHealth each frame, because Health has no damage event.

