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
        private Vector3 originalPosition;
        private bool sentinelIsIsolated;

        private void Start() => originalPosition = sentinel.transform.position;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
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
            GUILayout.BeginArea(new Rect(12, 12, 420, 260), GUI.skin.box);
            GUILayout.Label($"Selected action: {repairer.SelectedAction}");
            GUILayout.Label($"Ally: {(repairer.RepairTarget ? repairer.RepairTarget.name : "none")}    Threat: {repairer.ThreatSeconds:F1} s");
            ScoreRow("Hold", repairer.HoldScore, RepairerAction.Hold);
            ScoreRow("Repair", repairer.RepairScore, RepairerAction.Repair);
            ScoreRow("Escape", repairer.EscapeScore, RepairerAction.Escape);
            ScoreRow("Return", repairer.ReturnScore, RepairerAction.Return);
            var robotHealth = repairer.GetComponent<Health>();
            GUILayout.Label($"Repairer HP: {robotHealth.CurrentHealth}/{robotHealth.MaxHealth}");
            GUILayout.Label($"Sentinel HP: {sentinel.CurrentHealth}/{sentinel.MaxHealth}");
            if (!string.IsNullOrEmpty(repairer.Status)) GUILayout.Label(repairer.Status);
            GUILayout.Label("1  Damage Sentinel   |   2  Toggle isolated island");
            GUILayout.Label("Left click  Fire pulse gun");
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
