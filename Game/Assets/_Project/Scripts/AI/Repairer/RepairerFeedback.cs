using UnityEngine;

namespace FacilityEscape.AI.Repairer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RepairerAI))]
    public sealed class RepairerFeedback : MonoBehaviour
    {
        [SerializeField] private Transform emitter;
        [SerializeField] private Material beamMaterial;
        [SerializeField, Min(0.01f)] private float pulseSeconds = 0.18f;
        private RepairerAI repairer;
        private LineRenderer beam;

        private void Awake()
        {
            repairer = GetComponent<RepairerAI>();
            beam = gameObject.AddComponent<LineRenderer>();
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.startWidth = beam.endWidth = 0.04f;
            beam.sharedMaterial = beamMaterial;
            beam.enabled = false;
        }

        private void Update()
        {
            bool active = emitter != null && beamMaterial != null &&
                repairer.CanRepairCurrentTarget && Time.time - repairer.LastHealTime < pulseSeconds &&
                repairer.RepairTarget.CurrentHealth < repairer.RepairTarget.MaxHealth;
            beam.enabled = active;
            if (!active) return;
            beam.SetPosition(0, emitter.position);
            beam.SetPosition(1, repairer.RepairTarget.transform.position + Vector3.up * 0.9f);
        }

        private void OnDisable()
        {
            if (beam != null) beam.enabled = false;
        }
    }
}
