using UnityEngine;
using FacilityEscape.Combat;

namespace FacilityEscape.Feedback
{
    /// <summary>
    /// Adds a tracer, gun sound, crosshair target info and hit marker to the team's PulseGun
    /// without modifying it, by watching its public ShotsFired counter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PulseGunFeedback : MonoBehaviour
    {
        [SerializeField] private PulseGun gun;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameHud hud;
        [SerializeField, Min(1f)] private float range = 50f;
        [SerializeField] private Vector3 muzzleOffset = new Vector3(0.25f, -0.2f, 0.6f);
        [SerializeField] private Color tracerColor = new Color(0.4f, 1f, 1f);
        [SerializeField, Min(0.005f)] private float tracerWidth = 0.025f;
        [SerializeField, Min(0.01f)] private float tracerSeconds = 0.06f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private LineRenderer tracer;
        private Transform playerRoot;
        private int lastShots;
        private float tracerUntil;

        private bool aimHit;
        private Vector3 aimPoint;
        private Collider cachedCollider;
        private Health cachedHealth;
        private Health aimedHealth;
        private bool aimedAlive;

        private void Awake()
        {
            tracer = gameObject.AddComponent<LineRenderer>();
            tracer.positionCount = 2;
            tracer.useWorldSpace = true;
            tracer.enabled = false;
            tracer.startWidth = tracerWidth;
            tracer.endWidth = tracerWidth;
            tracer.startColor = tracerColor;
            tracer.endColor = tracerColor;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) tracer.material = new Material(shader);
        }

        private void Start()
        {
            if (hud == null) hud = FindFirstObjectByType<GameHud>();

            if (gun != null)
            {
                lastShots = gun.ShotsFired;
                CharacterController controller = gun.GetComponentInParent<CharacterController>();
                playerRoot = controller != null ? controller.transform : null;
            }

            if (gun == null || playerCamera == null)
                Debug.LogWarning("PulseGunFeedback needs the PulseGun and the player Camera assigned.", this);
        }

        private void Update()
        {
            if (gun == null || playerCamera == null) return;

            // Handle the shot first, using what the crosshair was over on the previous frame
            // (a target killed this frame is already deactivated and would no longer be hit).
            if (gun.ShotsFired > lastShots)
            {
                lastShots = gun.ShotsFired;
                OnShot();
            }

            UpdateAim();

            if (tracer.enabled && Time.time >= tracerUntil) tracer.enabled = false;
        }

        private void OnShot()
        {
            Transform cam = playerCamera.transform;
            Vector3 start = cam.TransformPoint(muzzleOffset);
            Vector3 end = aimHit ? aimPoint : cam.position + cam.forward * range;

            tracer.SetPosition(0, start);
            tracer.SetPosition(1, end);
            tracer.enabled = true;
            tracerUntil = Time.time + tracerSeconds;

            Sfx.Play2D(SfxId.PulseShot, 0.8f);

            if (aimedHealth != null && aimedAlive)
            {
                bool killed = aimedHealth.IsDestroyed;
                if (hud != null) hud.ShowHitMarker(killed);

                if (killed) Sfx.Play(SfxId.Death, aimPoint);
                else Sfx.Play2D(SfxId.HitTick);
            }
        }

        private void UpdateAim()
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int count = Physics.RaycastNonAlloc(ray, hitBuffer, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            Collider bestCollider = null;
            aimHit = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hitBuffer[i];
                if (playerRoot != null && hit.collider.transform.IsChildOf(playerRoot)) continue;
                if (hit.distance >= best) continue;

                best = hit.distance;
                aimPoint = hit.point;
                bestCollider = hit.collider;
                aimHit = true;
            }

            if (bestCollider != cachedCollider)
            {
                cachedCollider = bestCollider;
                cachedHealth = bestCollider != null ? bestCollider.GetComponentInParent<Health>() : null;
            }

            aimedHealth = cachedHealth;
            aimedAlive = aimedHealth != null && !aimedHealth.IsDestroyed;

            if (hud != null) hud.SetAim(aimedAlive ? aimedHealth : null);
        }
    }
}