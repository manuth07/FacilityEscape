using UnityEngine;
using FacilityEscape.Combat;
using FacilityEscape.Feedback;

namespace FacilityEscape.AI.Repairer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RepairerAI))]
    public sealed class RepairerRepairBeam : MonoBehaviour
    {
        [SerializeField] private LineRenderer beam;
        [SerializeField] private Vector3 targetOffset = new(0f, 0.8f, 0f);

        [SerializeField] private Transform shoulder;
        private Quaternion idlePose;
        private float extension;
        private float idleWeight;

        private RepairerAI repairer;
        private Health health;
        private int previousHp;
        private float flashUntil;
        private MeshRenderer[] surfaces;
        private MaterialPropertyBlock tint;
        private MaterialPropertyBlock saved;
        private MeshRenderer flashingSurface;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            tint = new MaterialPropertyBlock();
            saved = new MaterialPropertyBlock();
            health = GetComponent<Health>();
            previousHp = health.CurrentHealth;
            surfaces = GetComponentsInChildren<MeshRenderer>();
            if (!TryGetComponent<FloatingHealthBar>(out _))
                gameObject.AddComponent<FloatingHealthBar>();
        }

        private void UpdateHitFlash()
        {
            if (health.CurrentHealth < previousHp)
            {
                flashUntil = Time.time + 0.16f;
                if (!flashingSurface)
                {
                    // Tint one visible body panel, preserving its existing property overrides.
                    foreach (var surface in surfaces)
                    {
                        if (!surface.enabled || !surface.gameObject.activeInHierarchy) continue;
                        if (!flashingSurface || surface.bounds.size.sqrMagnitude > flashingSurface.bounds.size.sqrMagnitude)
                            flashingSurface = surface;
                    }
                    if (flashingSurface)
                    {
                        flashingSurface.GetPropertyBlock(saved);
                        flashingSurface.GetPropertyBlock(tint);
                        tint.SetColor(BaseColor, new Color(1f, 0.25f, 0.08f));
                        flashingSurface.SetPropertyBlock(tint);
                    }
                }
            }
            previousHp = health.CurrentHealth;
            if (Time.time >= flashUntil) ClearFlash();
        }

        private void ClearFlash()
        {
            if (!flashingSurface) return;
            flashingSurface.SetPropertyBlock(saved);
            flashingSurface = null;
        }

        private void Awake()
        {
            repairer = GetComponent<RepairerAI>();
            if (shoulder) idlePose = shoulder.localRotation;
            if (beam == null) enabled = false;
            else beam.enabled = false;
        }

        private void LateUpdate()
        {
            UpdateHitFlash();
            var target = repairer.ActiveRepairTarget;
            if (shoulder)
            {
                extension = Mathf.MoveTowards(extension, target ? 1f : 0f, 4f * Time.deltaTime);
                float eased = Mathf.SmoothStep(0f, 1f, extension);
                bool resting = !target && repairer.SelectedAction == RepairerAction.Hold &&
                    repairer.ThreatSeconds <= 0f;
                idleWeight = Mathf.MoveTowards(idleWeight, resting ? 1f : 0f, 3f * Time.deltaTime);
                float idlePitch = 1.5f * idleWeight * Mathf.Sin(Time.time * 2f);
                shoulder.localRotation = idlePose * Quaternion.AngleAxis(-24f * eased + idlePitch, Vector3.right);
            }
            beam.enabled = target != null;
            if (target == null) return;

            beam.SetPosition(0, beam.transform.position);
            beam.SetPosition(1, target.transform.TransformPoint(targetOffset));
            beam.widthMultiplier = 0.035f + 0.008f * Mathf.Sin(Time.time * 20f);
        }

        private void OnDisable()
        {
            ClearFlash();
            extension = 0f;
            idleWeight = 0f;
            if (shoulder) shoulder.localRotation = idlePose;
            if (beam != null) beam.enabled = false;
        }
    }
}
