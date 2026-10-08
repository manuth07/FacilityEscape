# Facility Escape

Our Unity group project is in `Game/` (relative to the repository root).

- Requested Unity Editor version: **6000.3.24f1**.
- Actual project version in `Game/ProjectSettings/ProjectVersion.txt`: **6000.6.2f1**. This differs from the requested version; the project has not been upgraded or downgraded. Use the recorded project version until the team agrees on a version change.
- Blender: **4.5.14 LTS**.

Teammates should clone this repository, then use Unity Hub to add and open the `Game/` project with its recorded Editor version.

`Game/Assets/_Project/` contains our own assets, organized into Scenes, Scripts, Prefabs, Models, Materials, Audio, and UI. `Game/Assets/ThirdParty/` contains downloaded assets. Keep original Blender work in `ArtSource/Blender/` and project documentation in `Docs/`.

Each shared scene or Blender model should have one editor at a time; coordinate with the team before editing.

After creating folders on disk, return to Unity and let it import them and generate their folder `.meta` files. Include those Unity-generated files in version control alongside assets; do not create GUIDs or `.meta` files manually.
