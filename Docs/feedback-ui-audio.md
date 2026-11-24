\# HUD, gun feedback and sound effects



\## Scope and status

These are optional additions built on top of the assigned work (interaction physics and the Flanker). They are not part

of any individual role in the assignment, so they live on a separate branch (feat/feedback-ui-audio) and are merged only

with the team's agreement.

\- Everything here only reads state from other systems. No teammate's script is modified.



\## What the player sees

\- Player health bar (bottom left): number, colour from green to yellow to red, a white trail that shows recent damage,

&#x20; a red screen flash on damage, and a pulsing tint below 30 percent health.

\- Target health bar (top centre): the name and health of whatever the crosshair is over (the Flanker, the Hunter, the

&#x20; test dummy, any object with a Health). It stays visible for 2 s after the crosshair leaves.

\- Crosshair that turns red over a target, and a hit marker (white for a hit, red for a kill).

\- Noise devices left and power cells collected (bottom right). "SYSTEM FAILURE" when the player's health reaches 0.

\- Optional floating health bar above an agent, shown for 3 s after it is damaged.



\## Design principle: observe, do not edit

The gun, Health, PlayerThrower and PowerCellManager belong to other parts of the project, so the feedback code reads their

public state instead of changing them:

\- PulseGun.ShotsFired (a public counter) tells the feedback script when a shot happened.

\- Health.CurrentHealth, MaxHealth and IsDestroyed drive every health bar.

\- PlayerThrower.Devices and PowerCellManager.CollectedCount drive the counters.

This avoids merge conflicts and keeps each feature removable without breaking the rest of the game.



\## HUD (GameHud)

\- Built at runtime from code (Canvas, Image, TextMeshPro text), so there is no scene wiring to break during a merge.

\- Canvas scaling follows the screen size (reference 1920 x 1080), so it keeps its proportions at any resolution.

\- Bars are rectangles whose right anchor moves, so no sprites are needed. The damage trail follows the fill after a delay.

\- It finds the player's Health through the FirstPersonController, and the other references by type if they are not assigned.

\- Text uses TextMeshPro (signed-distance-field rendering) instead of the legacy Text component, so it stays sharp when scaled.

&#x20; This needs the TMP Essential Resources (Window > TextMeshPro > Import TMP Essential Resources), which adds Assets/TextMesh Pro.



\## Gun feedback (PulseGunFeedback)

\- Watches PulseGun.ShotsFired. When it increases, it draws a short cyan tracer (a LineRenderer, 0.06 s), plays the gun sound,

&#x20; and shows a hit marker if the crosshair was on something with a Health.

\- It repeats the gun's own aim ray each frame, from the centre of the screen, ignoring the player's own colliders. That keeps the

&#x20; marker consistent with what the gun hit.

\- A shot is handled using the aim from the previous frame, because Health deactivates a killed target in the same frame as the shot,

&#x20; so a fresh raycast would no longer see it and the kill marker would be missed.

\- Optimisation: Physics.RaycastNonAlloc with a reusable 16-hit buffer (no garbage each frame), and the Health lookup is cached per

&#x20; collider, so GetComponentInParent runs only when the aimed collider changes.



\## Floating health bar (FloatingHealthBar)

\- A world-space canvas above the agent, rotated each frame to face the active camera.

\- Hidden until the agent is damaged, then shown for 3 s. It can be set to always visible.

\- It is a separate component, so it can be added to or removed from any agent that has a Health.



\## Sound effects

\### Generated sounds (Sfx)

Every effect has a generated version, made from maths (frequency sweeps, decaying envelopes, filtered noise) on first use and cached.

There are no audio files or licences needed for these.

\- Sounds: pulse shot, enemy shot, hit tick, door open, door close, door denied, device landing, device beep,

&#x20; death, Flanker alert, power cell pickup, crate scrape, crate impact.

\- Sfx.Play plays a 3D sound at a world position (volume falls with distance). Sfx.Play2D plays at the listener, so it is always heard.

\- Sfx.GetClip returns the clip for a sound so other scripts can use it in their own AudioSource.



\### Custom clips (SfxBank)

\- SfxBank holds named slots for clips downloaded from Pixabay. Any empty slot falls back to the generated sound, so the game is

&#x20; never silent and a missing file is never an error.

\- A master volume slider scales every sound.

\- Import settings for the clips: Force To Mono (the sounds are positioned in 3D), Load Type Decompress On Load for short clips,

&#x20; Vorbis compression. The looping crate scrape uses Compressed In Memory.

\- Licence: the clips come from Pixabay and are used under the Pixabay Content License (free for commercial and non-commercial

&#x20; use, attribution not required but given). Sources are listed in docs/credits.md.



\### Where sounds are triggered

\- Door: open, close, and a denied beep on the locked exit door.

\- NoiseDevice: a thud when it lands, then a beep with each sound event.

\- FlankerAI: a short two-tone alert when it first notices the player.

\- FlankerAttack: a shot sound at the Flanker's position.

\- GameHud: a hurt sound when the player's health drops.

\- PulseGunFeedback: gun shot, hit tick and death sound.

\- PowerCellFeedback: a pickup sound when PowerCellManager.CollectedCount increases (it observes the count, so the power cell

&#x20; code is untouched).

\- CrateAudio: a looping scrape whose volume follows the crate's horizontal speed (it fades in and out, and stops completely when

&#x20; the crate is still), plus a thud on collisions above 0.8 m/s with a 0.15 s cooldown so many collisions do not stack. Its

&#x20; AudioSource is created in Start, after the SfxBank has registered in Awake, and uses linear 3D roll-off from 2 m to 18 m.



\## Scene setup

\- An Audio object with the SfxBank component and the clips assigned.

\- A GameHud object with the GameHud and PowerCellFeedback components.

\- PulseGunFeedback on the PlayerCamera, with the PulseGun, the player camera and the GameHud assigned.

\- CrateAudio on the Crate prefab, so every crate has it.

\- An Audio Listener on the active player camera (and only one in the scene).

\- A Health component on the Player (the shared Health deactivates the object at 0, so what "game over" does is a team decision).



\## Files

Game/Assets/\_Project/Scripts/Feedback/

\- Sfx.cs: sound ids, generated clips, bank lookup, Play and Play2D

\- SfxBank.cs: named clip slots and master volume

\- GameHud.cs: runtime-built HUD

\- PulseGunFeedback.cs: tracer, gun sound, aim target and hit marker

\- FloatingHealthBar.cs: world-space health bar above an agent

\- PowerCellFeedback.cs: power cell pickup sound

\- CrateAudio.cs: crate scrape and impact sounds



Edited to call Sfx: Door.cs, NoiseDevice.cs, FlankerAI.cs, FlankerAttack.cs. The old on-screen device counter was removed from

PlayerThrower because the HUD now shows it.



\## Optimisation

\- Text is only rewritten when a value changes (device and power cell counters).

\- RaycastNonAlloc with a reusable buffer, and a cached Health lookup per aimed collider.

\- Generated clips are built once and reused.

\- Short clips decompress on load and are mono, so they play without a delay and cost little memory.

\- A looping scrape source stops completely when silent, instead of playing at zero volume.

\- Impact sounds have a cooldown.



\## Testing

Checked in Play mode with no errors in the Console:

\- HUD text is sharp with the Game view at 1x scale, and stays sharp when the Game view is resized.

\- Player health bar, damage trail, red flash and low-health pulse; SYSTEM FAILURE at 0.

\- Target bar shows the Flanker's name and health while aiming, and hides after 2 s.

\- Crosshair turns red over a target; hit marker on a hit and a different one on a kill.

\- Door, noise device, power cell, crate, Flanker and gun sounds play at the right moments, and 3D sounds fade with distance.

\- An empty SfxBank slot falls back to the generated sound.

\- Doors, crates, the noise device, the Hunter and the Flanker behave as before (no regression), with no change in frame rate.



\## Known limitations

\- The HUD uses flat colours and a default font. A designer could restyle it with sprites and a custom font asset.

\- The generated sounds are simple; real clips replace them wherever a slot is filled.

\- The target bar and gun feedback need a Health component on whatever is aimed at.

\- The shared Health deactivates an object at 0, so a dead player disappears instead of showing a game-over screen. That needs a

&#x20; team decision and is not implemented here.

\- Sounds are not mixed through an AudioMixer, so there are no separate volume sliders for effects and music.



\## Credits

Sound effects: Pixabay 

