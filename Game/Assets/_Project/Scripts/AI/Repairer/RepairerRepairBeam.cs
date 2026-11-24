using UnityEngine;

namespace FacilityEscape.AI.Repairer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RepairerAI))]
    public sealed class RepairerRepairBeam : MonoBehaviour
    {
        [SerializeField] private LineRenderer beam;
        [SerializeField] private Vector3 targetOffset = new(0f, 0.8f, 0f);

        private RepairerAI repairer;

        private void Awake()
        {
            repairer = GetComponent<RepairerAI>();
            if (beam == null) enabled = false;
            else beam.enabled = false;
        }

        private void LateUpdate()
        {
            var target = repairer.ActiveRepairTarget;
            beam.enabled = target != null;
            if (target == null) return;

            beam.SetPosition(0, beam.transform.position);
            beam.SetPosition(1, target.transform.TransformPoint(targetOffset));
            beam.widthMultiplier = 0.035f + 0.008f * Mathf.Sin(Time.time * 20f);
        }

        private void OnDisable()
        {
            if (beam != null) beam.enabled = false;
        }
    }
}
