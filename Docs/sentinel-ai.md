# Member 4 — Sentinel

Open the `Game/` project in Unity Editor **6000.6.2f1**. After import, use **Facility Escape → Create Sentinel Test**. This generates the scene, Sentinel prefab, materials and baked NavMesh. Unity also generates the associated `.meta` files. On subsequent runs, open `Assets/_Project/Scenes/SentinelTest.unity` directly.

## Record the behaviour and animation

1. Select **Sentinel** in the Hierarchy and switch the Inspector to **Debug** if **Active Branch** is hidden. Start Play mode and give the Game view focus.
2. From the starting position, let the Sentinel fire once. Show **Attack** and the recoil and muzzle flash.
3. Aim at its body and left-click once. Wait for it to reach the orange pad behind Cover A. Show **Seek cover**, then **Hold cover**.
4. Strafe left into the west lane, then move forward along that lane. When you can see around the west end of the orange wall, pause there. Show **Relocate** and the movement to the teal pad behind Cover B.
5. Return south and wait about five seconds after the last sighting. Show **Guard** and the Sentinel returning to its original post. If taking another run, stop and re-enter Play mode.

The yellow cell model marks the objective for this test. Capture the Game view and the Inspector together so the marker colours, smooth turning and branch transitions are visible.

## Add it to the factory

1. Open `Assets/_Project/Scenes/FacilityEscape_Main.unity` in Unity Editor **6000.6.2f1**.
2. Choose **Facility Escape → Place Sentinel in Factory**. The tool finds Power Cell C and the existing player, uses the factory's baked robot NavMesh, and checks for at least two reachable cover points. If the prefab is missing, Unity generates it.
3. Check the Console for the placement message, then save the scene. The Hierarchy shows **Sentinel guard station** with the prefab, guard point and cover markers.
4. Enter Play mode and approach Power Cell C from the main factory. Shoot the Sentinel once. Try the northern or southern lane to expose its cover. Watch **Active Branch** and confirm it can navigate to the alternative marker without leaving the security area.
5. Record the tested sequence with the Game view and Inspector visible. Commit the saved factory scene, generated prefab and materials, and their Unity-generated `.meta` files after the factory test.

## Implementation

The priority selector tries **Relocate**, **Seek cover**, **Attack**, **Hold cover**, then **Guard**. Cover must block the last observed threat position and be reachable by a complete path inside the guard radius. The shortest valid route wins. A cover move keeps its destination through brief losses of sight and re-evaluates it when it becomes exposed or unreachable.

The agent owns position and acceleration. The motion component turns toward travel or aim and uses measured agent speed to animate the visible body. Each successful shot applies player damage and triggers recoil, muzzle flash and the existing enemy-shot sound.
