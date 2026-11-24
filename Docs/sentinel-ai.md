# Member 4 — Sentinel

Open the `Game/` project in Unity Editor **6000.6.2f1**. After import, use **Facility Escape → Create Sentinel Test**. This generates the scene, Sentinel prefab, materials and baked NavMesh. Unity also generates the associated `.meta` files. On subsequent runs, open `Assets/_Project/Scenes/SentinelTest.unity` directly.

## Record the behaviour and animation

1. Select **Sentinel** in the Hierarchy and switch the Inspector to **Debug** if **Active Branch** is hidden. Start Play mode and give the Game view focus.
2. From the starting position, let the Sentinel fire once. Show **Attack** and the recoil and muzzle flash.
3. Aim at its body and left-click once. Wait for it to reach the orange pad behind Cover A. Show **Seek cover**, then **Hold cover**.
4. Strafe left into the west lane, then move forward along that lane. When you can see around the west end of the orange wall, pause there. Show **Relocate** and the movement to the teal pad behind Cover B.
5. Return south and wait about five seconds after the last sighting. Show **Guard** and the Sentinel returning to its original post. If taking another run, stop and re-enter Play mode.

The yellow cell model marks the objective for this test. Capture the Game view and the Inspector together so the marker colours, smooth turning and branch transitions are visible.

## Test the integrated factory Sentinel

1. In Unity **6000.6.2f1**, open `Assets/_Project/Scenes/FacilityEscape_Main.unity` and expand **Sentinel guard station** in the Hierarchy. Select **Sentinel** to show **Active Branch**.
2. In Play Mode, approach Power Cell C. Listen for the warning before the pulse, then check the muzzle flash, tracer, and recoil. Break line of sight during a warning and verify that the pulse is cancelled and your health stays the same.
3. Approach again and shoot the Sentinel once. Use a side lane to expose its first cover spot. Capture **Relocate** and the movement to a second spot. In the Scene view, cyan shows the destination and planned path, green shows occupied cover, and the threat ray changes from red for a visible player to orange for a remembered position.
4. Listen near and far from the Sentinel. The shot comes from the muzzle with distance falloff, and repeated shots have slight pitch variation. The prefab uses the existing Ribhav Agrawal **Laser Shot Ingame** sample. To swap it, set **Shot Clip** on **Sentinel Motion**. A blank field uses the team's sound bank.
5. Stop and restart Play Mode to confirm the muzzle effects and recoil reset cleanly. Check the Console for new errors.

The factory scene already includes the guard station. The **Place Sentinel in Factory** menu command can restore it if it is removed while editing the scene.

## Implementation

The priority selector tries **Relocate**, **Seek cover**, **Attack**, **Hold cover**, then **Guard**. Cover must block the last observed threat position and be reachable by a complete path inside the guard radius. The shortest valid route wins. A cover move keeps its destination through brief losses of sight and re-evaluates it when it becomes exposed or unreachable.

The agent owns position and acceleration. The motion component turns toward travel or aim and uses measured agent speed to animate the visible body. Each successful shot applies player damage and triggers recoil, muzzle flash, a tracer and a spatial pulse sound after an interruptible warning.
