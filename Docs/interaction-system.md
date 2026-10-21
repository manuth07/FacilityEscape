\# Interaction system (Systems Engineer)



\## What it does

\- Press E to open and close doors (IInteractable + PlayerInteractor).

\- The exit door starts locked and unlocks when PowerCellManager reports all cells collected (ExitDoorUnlocker).

\- Walk into crates to push them (CratePusher). Crates carry a NavMeshObstacle with Carve, so robots reroute around barricades.

\- Press G to throw a noise device (PlayerThrower + NoiseDevice). It emits SoundEvents that the Hunter hears.



\## Files

\- Game/Assets/\_Project/Scripts/Interaction/

\- Game/Assets/\_Project/Prefabs/Interaction/

\- Test scene: Game/Assets/Scenes/InteractionTest.unity



\## Design choices

\- Interface (IInteractable): any object can be used without the player script knowing its type.

\- Raycast from the camera: matches what the player looks at, one cheap check per frame.

\- Rigidbody forces for crates: real collisions and stacking.

\- Shared SoundEvents: no second noise system, the Hunter decides whether to investigate.

\- Input is gated on cursor lock, matching PulseGun.



\## Optimisation

\- Door.Update exits early once the door reaches its target.

\- Box colliders on crates, no mesh colliders.

\- Noise devices destroy themselves after landing.

\- Layer mask on the interaction raycast.

