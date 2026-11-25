using FacilityEscape.Combat;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace FacilityEscape.AI.Repairer
{
    public sealed class RepairerDemo : MonoBehaviour
    {
        [SerializeField] private RepairerAI repairer;
        [SerializeField] private Health sentinel;
        [SerializeField] private Transform isolatedPosition;
        [SerializeField] private Transform sentinelGuard;
        [SerializeField] private GameObject powerCellModel;
        [SerializeField] private Light displayLight;
        private float normalLightIntensity;
        private bool dimLight;
        private Vector3 originalPosition;
        private bool sentinelIsIsolated;

        public enum Scenario { Single, Competing, Blocked }
        private static Scenario nextScenario;
        private Health competingAlly;
        private GameObject barrier;
        private bool resetting;
        private Health repairerHealth;
        private GUIStyle instructionStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetScenario() => nextScenario = Scenario.Single;

        public void Restart(Scenario scenario)
        {
            if (resetting) return;
            nextScenario = scenario;
            resetting = true;
            var scene = gameObject.scene;
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        private void PrepareScenario()
        {
            if (nextScenario == Scenario.Competing)
            {
                var ally = new GameObject("Maintenance drone");
                ally.transform.position = sentinel.transform.position + Vector3.right * 2.5f;
                var hull = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                hull.name = "Service hull";
                hull.transform.SetParent(ally.transform, false);
                hull.transform.localPosition = Vector3.up;
                competingAlly = ally.AddComponent<Health>();
                ally.AddComponent<RepairableAlly>();
                ally.AddComponent<FacilityEscape.Feedback.FloatingHealthBar>();
                competingAlly.TakeDamage(40);
                DamageSentinel();
            }
            else if (nextScenario == Scenario.Blocked)
            {
                barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barrier.name = "Retractable maintenance barrier";
                barrier.transform.position = new Vector3(0f, 1f, 0.5f);
                barrier.transform.localScale = new Vector3(17f, 2f, 0.45f);
                var obstacle = barrier.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
                DamageSentinel();
            }
        }

        public void ToggleBarrier()
        {
            if (barrier) barrier.SetActive(!barrier.activeSelf);
        }

        public void DamageCompetitor()
        {
            if (competingAlly)
                competingAlly.TakeDamage(Mathf.Min(25, competingAlly.CurrentHealth - 1));
        }

        private void Start()
        {
            repairerHealth = repairer.GetComponent<Health>();
            originalPosition = sentinel.transform.position;
            PrepareScenario();
            if (powerCellModel)
                Instantiate(powerCellModel, new Vector3(-1f, 0.5f, -3f), Quaternion.identity, transform);
            if (displayLight) normalLightIntensity = displayLight.intensity;
        }

        public void ToggleDisplayLight()
        {
            if (!displayLight) return;
            dimLight = !dimLight;
            displayLight.intensity = normalLightIntensity * (dimLight ? 0.08f : 1f);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) { Restart(Scenario.Single); return; }
            if (keyboard.f2Key.wasPressedThisFrame) { Restart(Scenario.Competing); return; }
            if (keyboard.f3Key.wasPressedThisFrame) { Restart(Scenario.Blocked); return; }
            if (keyboard.rKey.wasPressedThisFrame) { Restart(nextScenario); return; }
            if (keyboard.bKey.wasPressedThisFrame) ToggleBarrier();
            if (keyboard.digit3Key.wasPressedThisFrame) DamageCompetitor();
            if (keyboard.lKey.wasPressedThisFrame) ToggleDisplayLight();
            if (keyboard.digit1Key.wasPressedThisFrame) DamageSentinel();
            if (keyboard.digit2Key.wasPressedThisFrame) ToggleSentinelReachability();
        }

        public void DamageSentinel() => sentinel.TakeDamage(Mathf.Min(65, sentinel.CurrentHealth - 1));

        public void ToggleSentinelReachability()
        {
            Vector3 destination = !sentinelIsIsolated ?
                isolatedPosition.position : originalPosition;
            if (sentinel.TryGetComponent(out NavMeshAgent agent))
            {
                if (!agent.Warp(destination)) return;
                if (sentinelGuard != null) sentinelGuard.position = agent.nextPosition;
            }
            else
            {
                sentinel.transform.position = destination;
            }
            sentinelIsIsolated = !sentinelIsIsolated;
        }

        private void OnGUI()
        {
            instructionStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(440f, Screen.width - 24f), Mathf.Min(384f, Screen.height - 24f)), GUI.skin.box);
            GUILayout.Label($"Repairer showcase  |  {nextScenario}");
            GUILayout.Label("F1 Single   F2 Competing   F3 Blocked   R Reset");
            string instruction = nextScenario switch
            {
                Scenario.Competing => "Press 3 to make the maintenance drone more urgent. Watch the selected ally.",
                Scenario.Blocked => "Press B to open the barrier. Watch for healing to start when the route clears.",
                _ => "Press 1 to start a repair. Fire nearby to interrupt it and watch the Repairer retreat."
            };
            GUILayout.Label(instruction, instructionStyle);
            GUILayout.Label($"Selected action: {repairer.SelectedAction}");
            GUILayout.Label($"Ally: {(repairer.RepairTarget ? repairer.RepairTarget.name : "none")}    Threat: {repairer.ThreatSeconds:F1} s");
            ScoreRow("Hold", repairer.HoldScore, RepairerAction.Hold);
            ScoreRow("Repair", repairer.RepairScore, RepairerAction.Repair);
            ScoreRow("Escape", repairer.EscapeScore, RepairerAction.Escape);
            ScoreRow("Return", repairer.ReturnScore, RepairerAction.Return);
            GUILayout.Label($"Repairer HP: {repairerHealth.CurrentHealth}/{repairerHealth.MaxHealth}");
            GUILayout.Label($"Sentinel HP: {sentinel.CurrentHealth}/{sentinel.MaxHealth}");
            if (competingAlly)
                GUILayout.Label($"Maintenance drone: {competingAlly.CurrentHealth}/{competingAlly.MaxHealth}    3  Damage drone");
            if (barrier)
                GUILayout.Label($"Barrier: {(barrier.activeSelf ? "closed" : "open")}    B  Toggle");
            if (!string.IsNullOrEmpty(repairer.Status)) GUILayout.Label(repairer.Status);
            GUILayout.Label("1  Damage Sentinel   |   2  Toggle isolated island");
            GUILayout.Label("WASD / mouse  Move / look   |   Click  Fire   |   L  Lighting");
            GUILayout.EndArea();
        }

        private void ScoreRow(string label, float score, RepairerAction action)
        {
            Color previous = GUI.contentColor;
            if (action == repairer.SelectedAction) GUI.contentColor = Color.cyan;
            GUILayout.Label($"{label}  {score:F2}");
            GUI.contentColor = previous;
        }
    }
}
