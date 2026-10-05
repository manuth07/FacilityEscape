using FacilityEscape.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FacilityEscape.AI.Repairer
{
    public sealed class RepairerDemo : MonoBehaviour
    {
        [SerializeField] private RepairerAI repairer;
        [SerializeField] private Health sentinel;
        [SerializeField] private Transform isolatedPosition;
        private Vector3 originalPosition;

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
            sentinel.transform.position = sentinel.transform.position == originalPosition ?
                isolatedPosition.position : originalPosition;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 350, 190), GUI.skin.box);
            GUILayout.Label($"Selected action: {repairer.SelectedAction}");
            ScoreRow("Repair", repairer.RepairScore, RepairerAction.Repair);
            ScoreRow("Escape", repairer.EscapeScore, RepairerAction.Escape);
            ScoreRow("Return", repairer.ReturnScore, RepairerAction.Return);
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
