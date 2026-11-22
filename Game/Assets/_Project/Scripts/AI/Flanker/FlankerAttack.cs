using UnityEngine;
using FacilityEscape.AI.Common;
using FacilityEscape.Combat;
using FacilityEscape.Feedback;

namespace FacilityEscape.AI.Flanker
{
    /// <summary>
    /// Ranged attack for the Flanker. It only fires while engaging with a clear line of sight,
    /// telegraphs each shot with an aim line, and is less accurate at long range.
    /// Kept separate from FlankerAI so movement decisions and combat stay decoupled.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FlankerAI))]
    public sealed class FlankerAttack : MonoBehaviour
    {
        [Header("Damage")]
        [SerializeField, Min(1)] private int damage = 10;
        [SerializeField, Min(0.2f)] private float cooldown = 1.5f;
        [SerializeField, Min(0f)] private float windUp = 0.6f;
        [SerializeField, Min(1f)] private float maxRange = 14f;

        [Header("Accuracy")]
        [SerializeField, Range(0f, 1f)] private float closeAccuracy = 0.85f;
        [SerializeField, Range(0f, 1f)] private float farAccuracy = 0.35f;
        [SerializeField, Min(0f)] private float missSpread = 1.2f;

        [Header("Shooting")]
        [SerializeField, Min(0f)] private float muzzleHeight = 1.4f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Sound")]
        [SerializeField, Min(0f)] private float shotHearingRadius = 12f;
        [SerializeField] private int shotPriority = 4; // below the Flanker's own threshold of 5

        [Header("Feedback")]
        [SerializeField, Min(0.01f)] private float tracerSeconds = 0.08f;
        [SerializeField] private bool logShots = true;
        [SerializeField] private bool showPlayerHealth = true;

        private FlankerAI flanker;
        private Health playerHealth;
        private LineRenderer tracer;
        private float nextShotTime;
        private float windUpEndsAt = -1f;
        private float flashUntil;
        private float nextLookupTime;
        private bool warned;

        private void Awake()
        {
            flanker = GetComponent<FlankerAI>();

            tracer = gameObject.AddComponent<LineRenderer>();
            tracer.positionCount = 2;
            tracer.useWorldSpace = true;
            tracer.enabled = false;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) tracer.material = new Material(shader);
        }

        private void OnDisable()
        {
            windUpEndsAt = -1f;
            if (tracer != null) tracer.enabled = false;
        }

        private void Update()
        {
            if (Time.time < flashUntil) return; // keep the shot line visible briefly

            bool aiming = Tick();
            if (!aiming && tracer.enabled) tracer.enabled = false;
        }

        /// <summary>Returns true while the aim line should be shown.</summary>
        private bool Tick()
        {
            if (!ResolvePlayerHealth() || playerHealth.IsDestroyed)
            {
                windUpEndsAt = -1f;
                return false;
            }

            if (flanker.State != FlankerAI.FlankerState.ENGAGE)
            {
                windUpEndsAt = -1f;
                return false;
            }

            Vector3 muzzle = transform.position + Vector3.up * muzzleHeight;
            Vector3 target = flanker.Player.bounds.center;
            float distance = Vector3.Distance(muzzle, target);

            if (distance > maxRange || !HasClearShot(muzzle, target))
            {
                windUpEndsAt = -1f;
                return false;
            }

            if (Time.time < nextShotTime) return false;

            if (windUpEndsAt < 0f) windUpEndsAt = Time.time + windUp;

            if (Time.time < windUpEndsAt)
            {
                SetTracer(muzzle, target, new Color(1f, 0.1f, 0.1f), 0.015f);
                return true; // telegraph: the player can still take cover
            }

            Fire(muzzle, target, distance);
            windUpEndsAt = -1f;
            nextShotTime = Time.time + cooldown;
            return false;
        }

        private void Fire(Vector3 muzzle, Vector3 target, float distance)
        {
            float accuracy = Mathf.Lerp(closeAccuracy, farAccuracy,
                                        Mathf.InverseLerp(0f, maxRange, distance));
            bool hit = Random.value <= accuracy;

            Vector3 end = target;
            if (hit) playerHealth.TakeDamage(damage);
            else end += Random.insideUnitSphere * missSpread;

            SetTracer(muzzle, end, new Color(1f, 0.6f, 0f), 0.06f);
            flashUntil = Time.time + tracerSeconds;

            SoundEvents.Emit(muzzle, shotHearingRadius, shotPriority, 1f);
            Sfx.Play(SfxId.EnemyShot, muzzle);

            if (logShots)
            {
                Debug.Log($"Flanker fired: {(hit ? "HIT" : "miss")} | accuracy {accuracy:F2}, " +
                          $"distance {distance:F1} m", this);
            }
        }

        private bool HasClearShot(Vector3 from, Vector3 to)
        {
            if (!Physics.Linecast(from, to, out RaycastHit hit, obstructionMask,
                                  QueryTriggerInteraction.Ignore))
                return true;

            return hit.collider.transform.IsChildOf(flanker.Player.transform);
        }

        private bool ResolvePlayerHealth()
        {
            if (playerHealth != null) return true;
            if (Time.time < nextLookupTime) return false;
            nextLookupTime = Time.time + 1f;

            CharacterController p = flanker.Player;
            if (p == null) return false;

            playerHealth = p.GetComponentInParent<Health>();
            if (playerHealth == null) playerHealth = p.GetComponentInChildren<Health>();

            if (playerHealth == null && !warned)
            {
                warned = true;
                Debug.LogWarning("FlankerAttack: the Player has no Health component, so it cannot be damaged.", this);
            }
            return playerHealth != null;
        }

        private void SetTracer(Vector3 from, Vector3 to, Color color, float width)
        {
            tracer.enabled = true;
            tracer.SetPosition(0, from);
            tracer.SetPosition(1, to);
            tracer.startColor = color;
            tracer.endColor = color;
            tracer.startWidth = width;
            tracer.endWidth = width;
        }

        private void OnGUI()
        {
            if (!showPlayerHealth || playerHealth == null) return;
            GUI.Label(new Rect(20, Screen.height - 70, 300, 30),
                      $"Player health: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}");
        }
    }
}