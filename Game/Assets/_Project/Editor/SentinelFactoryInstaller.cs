using System.Collections.Generic;
using System.Linq;
using FacilityEscape.AI.Sentinel;
using FacilityEscape.Combat;
using FacilityEscape.Gameplay;
using FacilityEscape.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FacilityEscape.EditorTools
{
    public static class SentinelFactoryInstaller
    {
        private const string FactoryPath = "Assets/_Project/Scenes/FacilityEscape_Main.unity";

        [MenuItem("Facility Escape/Place Sentinel in Factory")]
        public static void Place()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != FactoryPath)
            {
                Debug.LogWarning("Open FacilityEscape_Main outside Play mode before placing the Sentinel.");
                return;
            }

            if (FindInScene<SentinelAI>(scene).Any())
            {
                Debug.LogWarning("The factory scene already contains a Sentinel.");
                return;
            }

            var controller = FindInScene<FirstPersonController>(scene).FirstOrDefault();
            var player = controller != null ? controller.GetComponent<CharacterController>() : null;
            var objective = FindInScene<PowerCell>(scene).FirstOrDefault(cell => cell.name == "PowerCell_C");
            var surface = FindInScene<NavMeshSurface>(scene)
                .FirstOrDefault(candidate => candidate.isActiveAndEnabled && candidate.navMeshData != null);
            if (player == null || player.GetComponentInParent<Health>() == null ||
                objective == null || surface == null)
            {
                Debug.LogError("The factory needs its player with Health, PowerCell_C, and baked NavMesh.");
                return;
            }

            surface.AddData();
            var filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
            Vector3 floor = objective.transform.position;
            floor.y -= 0.6f;
            if (!NavMesh.SamplePosition(floor + Vector3.right * 1.6f, out NavMeshHit post, 0.55f, filter))
            {
                Debug.LogError("The baked robot NavMesh has no walkable point at the Sentinel guard post.");
                return;
            }

            var offsets = new[]
            {
                new Vector3(1.2f, 0, 2.6f), new Vector3(-4.35f, 0, 2.6f),
                new Vector3(1.2f, 0, -2.6f), new Vector3(-4.35f, 0, -2.6f)
            };
            var path = new NavMeshPath();
            var cover = new List<Vector3>();
            foreach (Vector3 offset in offsets)
            {
                if (NavMesh.SamplePosition(floor + offset, out NavMeshHit point, 0.55f, filter) &&
                    NavMesh.CalculatePath(post.position, point.position, filter, path) &&
                    path.status == NavMeshPathStatus.PathComplete)
                    cover.Add(point.position);
            }
            if (cover.Count < 2)
            {
                Debug.LogError("The Sentinel needs at least two reachable Security cover markers.");
                return;
            }

            GameObject prefab = SentinelDemoBuilder.GetOrCreatePrefab();
            if (prefab == null) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Place factory Sentinel");
            var setup = new GameObject("Sentinel guard station");
            Undo.RegisterCreatedObjectUndo(setup, "Create Sentinel guard station");
            Quaternion facing = Quaternion.LookRotation(Vector3.right);
            Transform guard = CreateMarker(setup.transform, "Guard point", post.position);
            guard.rotation = facing;

            var sentinel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, setup.transform);
            Undo.RegisterCreatedObjectUndo(sentinel, "Place Sentinel");
            sentinel.transform.SetPositionAndRotation(post.position, facing);
            var agent = sentinel.GetComponent<NavMeshAgent>();
            agent.agentTypeID = surface.agentTypeID;
            PrefabUtility.RecordPrefabInstancePropertyModifications(agent);
            PrefabUtility.RecordPrefabInstancePropertyModifications(sentinel.transform);

            var settings = new SerializedObject(sentinel.GetComponent<SentinelAI>());
            settings.FindProperty("player").objectReferenceValue = player;
            settings.FindProperty("guardPoint").objectReferenceValue = guard;
            SerializedProperty markers = settings.FindProperty("coverPoints");
            markers.arraySize = cover.Count;
            for (int i = 0; i < cover.Count; i++)
                markers.GetArrayElementAtIndex(i).objectReferenceValue =
                    CreateMarker(setup.transform, $"Security cover {i + 1}", cover[i]);

            settings.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = sentinel;
            Debug.Log($"Placed the Sentinel near PowerCell_C with {cover.Count} reachable cover markers.", sentinel);
        }

        private static Transform CreateMarker(Transform parent, string name, Vector3 position)
        {
            var marker = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(marker, "Create Sentinel marker");
            Undo.SetTransformParent(marker.transform, parent, "Parent Sentinel marker");
            marker.transform.position = position;
            return marker.transform;
        }

        private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>());
        }
    }
}
