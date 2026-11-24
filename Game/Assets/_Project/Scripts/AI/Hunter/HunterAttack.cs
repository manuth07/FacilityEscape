using UnityEngine;
using FacilityEscape.Combat;
using FacilityEscape.Feedback;

namespace FacilityEscape.AI.Hunter
{
    /// <summary>Shot timing and feedback; HunterAI owns decisions and navigation.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HunterAI), typeof(Health))]
    public sealed class HunterAttack : MonoBehaviour
    {
        [SerializeField] private CharacterController player;
        [SerializeField] private Health playerHealth;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Material pulseMaterial;
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField, Min(1f)] private float attackRange = 8f;
        [SerializeField, Min(1.1f)] private float preferredDistance = 5.5f;
        [SerializeField, Min(1)] private int damage = 10;
        [SerializeField, Min(0.1f)] private float cooldown = 1.5f;
        [SerializeField, Min(0f)] private float windUp = 0.35f;
        [SerializeField, Range(0f, 90f)] private float facingTolerance = 10f;
        [SerializeField, Min(0.01f)] private float pulseSeconds = 0.08f;

        private HunterAI hunter;
        private Health health;
        private LineRenderer pulse;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
        private float nextShotTime;
        private float windUpEndsAt = -1f;
        private float pulseEndsAt;

        public float AttackRange => attackRange;
        public float PreferredDistance => Mathf.Clamp(preferredDistance, 1.1f, attackRange);
        public bool HasLivingTarget => player != null && player.enabled && player.gameObject.activeInHierarchy &&
            playerHealth != null && playerHealth.isActiveAndEnabled && !playerHealth.IsDestroyed;
        public bool IsInRange => HasLivingTarget &&
            (player.transform.position - transform.position).sqrMagnitude <= attackRange * attackRange;
        public int ShotsFired { get; private set; }

        private void Awake()
        {
            hunter = GetComponent<HunterAI>();
            health = GetComponent<Health>();
            if (playerHealth == null && player != null)
                playerHealth = player.GetComponentInParent<Health>();
            pulse = gameObject.AddComponent<LineRenderer>();
            pulse.useWorldSpace = true;
            pulse.positionCount = 2;
            pulse.sharedMaterial = pulseMaterial;
            pulse.enabled = false;
        }

        private void Update()
        {
            if (pulse != null && Time.time >= pulseEndsAt) pulse.enabled = false;
            if (!CanFireAtTarget())
            {
                windUpEndsAt = -1f;
                if (pulse != null && Time.time >= pulseEndsAt) pulse.enabled = false;
                return;
            }
            if (Time.time < nextShotTime) return;
            if (windUpEndsAt < 0f) windUpEndsAt = Time.time + windUp;
            if (Time.time < windUpEndsAt)
            {
                ShowPulse(0.012f);
                return;
            }

            // Revalidate at the damage boundary: no cached LOS, stale target or extra tick.
            if (!CanFireAtTarget()) { windUpEndsAt = -1f; return; }
            nextShotTime = Time.time + cooldown;
            windUpEndsAt = -1f;
            if (!playerHealth.TakeDamage(damage)) return;
            ShotsFired++;
            ShowPulse(0.055f);
            pulseEndsAt = Time.time + pulseSeconds;
            Sfx.Play(SfxId.EnemyShot, muzzle.position);
        }

        private bool CanFireAtTarget()
        {
            if (!isActiveAndEnabled || health == null || health.IsDestroyed ||
                hunter == null || !hunter.isActiveAndEnabled ||
                hunter.State != HunterAI.HunterState.ATTACK || muzzle == null ||
                !IsInRange || !hunter.CanSeePlayer()) return false;
            Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            return Vector3.Angle(transform.forward, direction) <= facingTolerance && HasClearShot();
        }

        private bool HasClearShot()
        {
            Vector3 direction = player.transform.TransformPoint(player.center) - muzzle.position;
            int count = Physics.RaycastNonAlloc(muzzle.position, direction.normalized, hitBuffer,
                direction.magnitude + 0.05f, obstructionMask, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = count == hitBuffer.Length
                ? Physics.RaycastAll(muzzle.position, direction.normalized,
                    direction.magnitude + 0.05f, obstructionMask, QueryTriggerInteraction.Ignore)
                : hitBuffer;
            if (hits != hitBuffer) count = hits.Length;
            Collider nearest = null;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance >= distance) continue;
                distance = hits[i].distance;
                nearest = hits[i].collider;
            }
            return nearest != null && nearest.transform.IsChildOf(player.transform);
        }

        private void ShowPulse(float width)
        {
            if (pulse == null || pulseMaterial == null) return;
            pulse.startWidth = pulse.endWidth = width;
            pulse.SetPosition(0, muzzle.position);
            pulse.SetPosition(1, player.transform.TransformPoint(player.center));
            pulse.enabled = true;
        }

        private void OnDisable()
        {
            windUpEndsAt = -1f;
            if (pulse != null) pulse.enabled = false;
        }
    }
}
