using FacilityEscape.AI.Sentinel;
using FacilityEscape.Combat;
using FacilityEscape.Feedback;
using FacilityEscape.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityEscape.EditorTools
{
    public static class SentinelDemoBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/SentinelTest.unity";
        private const string PrefabPath = "Assets/_Project/Prefabs/Sentinel.prefab";
        private const string MaterialFolder = "Assets/_Project/Materials/SentinelDemo";

        [MenuItem("Facility Escape/Create Sentinel Test")]
        public static void Create()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ScenePath) != null)
            {
                EditorUtility.DisplayDialog("Sentinel Test",
                    "The demo scene already exists. Open the existing scene to play.",
                    "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "SentinelDemo");

            Material floor = MakeMaterial("Floor", new Color(0.24f, 0.29f, 0.36f));
            Material steel = MakeMaterial("Steel", new Color(0.14f, 0.25f, 0.34f));
            Material orange = MakeMaterial("Orange", new Color(1f, 0.46f, 0.1f));
            Material teal = MakeMaterial("Teal", new Color(0.08f, 0.72f, 0.7f));
            Material glow = MakeMaterial("Glow", new Color(1f, 0.91f, 0.34f));
            var geometry = new GameObject("Factory geometry");
            Box("Floor", geometry.transform, new Vector3(0, -0.25f, -2),
                new Vector3(18, 0.5f, 18), floor);
            Box("Cover A wall", geometry.transform, new Vector3(-3, 1.25f, -2),
                new Vector3(3.2f, 2.5f, 0.8f), orange);
            Box("Cover B wall", geometry.transform, new Vector3(1.65f, 1.25f, -0.4f),
                new Vector3(1f, 2.5f, 2f), teal);
            Box("Back wall", geometry.transform, new Vector3(0, 1.5f, 6.8f),
                new Vector3(18, 3, 0.4f), steel);
            Box("West wall", geometry.transform, new Vector3(-8.8f, 1.5f, -2),
                new Vector3(0.4f, 3, 18), steel);
            Box("East wall", geometry.transform, new Vector3(8.8f, 1.5f, -2),
                new Vector3(0.4f, 3, 18), steel);
            Box("Entrance wall", geometry.transform, new Vector3(0, 1.5f, -10.8f),
                new Vector3(18, 3, 0.4f), steel);
            Box("Power cell pedestal", geometry.transform, new Vector3(0, 0.45f, 3.5f),
                new Vector3(1.2f, 0.9f, 1.2f), steel);

            Box("Power cell display", null, new Vector3(0, 1.2f, 3.5f),
                new Vector3(0.5f, 0.6f, 0.5f), glow, false);
            Box("A marker", null, new Vector3(-3, 0.01f, 0),
                new Vector3(0.9f, 0.02f, 0.9f), orange, false);
            Box("B marker", null, new Vector3(3, 0.01f, 0),
                new Vector3(0.9f, 0.02f, 0.9f), teal, false);

            Light key = new GameObject("Factory lighting").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.2f;
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(51, -35, 0);

            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = new Vector3(0, 0.05f, -8);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.center = Vector3.up;
            controller.height = 2;
            player.AddComponent<Health>();

            Transform view = Marker("Camera", player.transform, new Vector3(0, 1.5f, 0));
            view.tag = "MainCamera";
            Camera camera = view.gameObject.AddComponent<Camera>();
            view.gameObject.AddComponent<AudioListener>();
            FirstPersonController movement = player.AddComponent<FirstPersonController>();
            var gun = new SerializedObject(player.AddComponent<PulseGun>());
            gun.FindProperty("playerCamera").objectReferenceValue = camera;
            gun.FindProperty("playerRoot").objectReferenceValue = player.transform;
            gun.FindProperty("playerMovement").objectReferenceValue = movement;
            gun.FindProperty("shotsPerSecond").floatValue = 1f;
            gun.ApplyModifiedPropertiesWithoutUndo();
            var feedback = new SerializedObject(player.AddComponent<PulseGunFeedback>());
            feedback.FindProperty("gun").objectReferenceValue = gun.targetObject;
            feedback.FindProperty("playerCamera").objectReferenceValue = camera;
            feedback.ApplyModifiedPropertiesWithoutUndo();
            var hud = new SerializedObject(new GameObject("HUD").AddComponent<GameHud>());
            hud.FindProperty("playerHealth").objectReferenceValue = player.GetComponent<Health>();
            hud.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = GetOrCreatePrefab();
            var sentinel = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Transform guard = Marker("Guard point", null, Vector3.zero);
            guard.rotation = Quaternion.Euler(0, 180, 0);
            sentinel.transform.rotation = guard.rotation;

            var ai = new SerializedObject(sentinel.GetComponent<SentinelAI>());
            ai.FindProperty("player").objectReferenceValue = controller;
            ai.FindProperty("guardPoint").objectReferenceValue = guard;
            SerializedProperty covers = ai.FindProperty("coverPoints");
            covers.arraySize = 2;
            covers.GetArrayElementAtIndex(0).objectReferenceValue =
                Marker("Cover A", null, new Vector3(-3, 0, 0));
            covers.GetArrayElementAtIndex(1).objectReferenceValue =
                Marker("Cover B", null, new Vector3(3, 0, 0));
            ai.ApplyModifiedPropertiesWithoutUndo();

            NavMeshSurface surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData, "Assets/_Project/Scenes/SentinelTestNavMesh.asset");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = sentinel;
        }

        internal static GameObject GetOrCreatePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null) return prefab;

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "SentinelDemo");

            return CreatePrefab(
                MakeMaterial("Steel", new Color(0.14f, 0.25f, 0.34f)),
                MakeMaterial("Orange", new Color(1f, 0.46f, 0.1f)),
                MakeMaterial("Glow", new Color(1f, 0.91f, 0.34f)));
        }

        private static GameObject CreatePrefab(Material steel, Material orange, Material glow)
        {
            var robot = new GameObject("Sentinel");
            NavMeshAgent agent = robot.AddComponent<NavMeshAgent>();
            agent.speed = 3f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.18f;
            agent.radius = 0.4f;
            agent.height = 1.8f;
            CapsuleCollider bodyCollider = robot.AddComponent<CapsuleCollider>();
            bodyCollider.height = 1.8f;
            bodyCollider.center = Vector3.up * 0.9f;
            bodyCollider.radius = 0.4f;
            robot.AddComponent<SentinelAI>();
            robot.AddComponent<FloatingHealthBar>();

            Transform body = Marker("Body", robot.transform, Vector3.zero);
            Box("Base", body, new Vector3(0, 0.33f, 0), new Vector3(0.78f, 0.45f, 0.7f), steel, false);
            Box("Chassis", body, new Vector3(0, 0.82f, 0),
                new Vector3(0.76f, 0.55f, 0.62f), orange, false);
            Box("Head", body, new Vector3(0, 1.36f, 0), new Vector3(0.66f, 0.45f, 0.58f), steel, false);
            Box("Visor", body, new Vector3(0, 1.38f, 0.3f), new Vector3(0.48f, 0.13f, 0.04f), glow, false);
            Transform arm = Marker("Pulse arm", body, new Vector3(0.4f, 1.03f, 0.2f));
            Box("Barrel", arm, Vector3.forward * 0.1f, new Vector3(0.19f, 0.2f, 0.55f), steel, false);
            Transform muzzle = Marker("Muzzle", arm, Vector3.forward * 0.4f);
            GameObject flash = Box("Muzzle flash", muzzle, Vector3.forward * 0.1f,
                new Vector3(0.18f, 0.18f, 0.26f), glow, false);
            flash.SetActive(false);

            var motion = new SerializedObject(robot.GetComponent<SentinelMotion>());
            motion.FindProperty("eye").objectReferenceValue =
                Marker("Eye", body, new Vector3(0, 1.38f, 0.34f));
            motion.FindProperty("muzzle").objectReferenceValue = muzzle;
            motion.FindProperty("recoilPart").objectReferenceValue = arm;
            motion.FindProperty("visualBody").objectReferenceValue = body;
            motion.FindProperty("muzzleFlash").objectReferenceValue = flash;
            motion.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(robot, PrefabPath);
            Object.DestroyImmediate(robot);
            return prefab;
        }

        private static Material MakeMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform Marker(string name, Transform parent, Vector3 position)
        {
            Transform marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.localPosition = position;
            return marker;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size,
            Material material, bool solid = true)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }
    }
}
