using FacilityEscape.Feedback;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityEscape.AI.Sentinel
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SentinelMotion : MonoBehaviour
    {
        [SerializeField] private Transform eye;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform recoilPart;
        [SerializeField] private Transform visualBody;
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField, Min(1)] private float turnSpeed = 540f;

        [Header("Pulse audio")]
        [SerializeField] private AudioClip shotClip;
        [SerializeField, Range(0f, 1f)] private float shotVolume = 0.85f;

        private NavMeshAgent agent;
        private AudioSource shotSource;
        private LineRenderer tracer;
        private Material tracerMaterial;
        private const float FlashDuration = 0.07f;
        private Vector3 bodyRest;
        private Vector3 recoilRest;
        private float speed;
        private float stride;
        private float shotTime = float.NegativeInfinity;

        public Vector3 EyePosition => eye != null ? eye.position : transform.position + Vector3.up * 1.4f;
        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : EyePosition;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.updateRotation = false;

            var emitter = new GameObject("Pulse audio");
            emitter.transform.SetParent(muzzle != null ? muzzle : transform, false);
            if (muzzle == null) emitter.transform.localPosition = Vector3.up * 1.4f;
            shotSource = emitter.AddComponent<AudioSource>();
            shotSource.playOnAwake = false;
            shotSource.spatialBlend = 1f;
            shotSource.rolloffMode = AudioRolloffMode.Linear;
            shotSource.minDistance = 2f;
            shotSource.maxDistance = 23f;
            shotSource.dopplerLevel = 0f;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                tracer = emitter.AddComponent<LineRenderer>();
                tracerMaterial = new Material(shader);
                tracer.sharedMaterial = tracerMaterial;
                tracer.useWorldSpace = true;
                tracer.positionCount = 2;
                tracer.startWidth = 0.055f;
                tracer.endWidth = 0.02f;
                tracer.startColor = new Color(1f, 0.75f, 0.3f);
                tracer.endColor = new Color(1f, 0.35f, 0.08f);
                tracer.enabled = false;
            }
            if (visualBody != null) bodyRest = visualBody.localPosition;
            if (recoilPart != null) recoilRest = recoilPart.localPosition;
            if (muzzleFlash != null) muzzleFlash.SetActive(false);
        }

        public void Face(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }

        public void BeginAim()
        {
            Sfx.Play(SfxId.Alert, MuzzlePosition, 0.45f);
        }

        public void Fire(Vector3 impactPoint)
        {
            shotTime = Time.time;
            if (tracer != null)
            {
                tracer.SetPosition(0, MuzzlePosition);
                tracer.SetPosition(1, impactPoint);
                tracer.enabled = true;
            }
            if (muzzleFlash != null) muzzleFlash.SetActive(true);
            AudioClip clip = shotClip != null ? shotClip : Sfx.GetClip(SfxId.EnemyShot);
            if (clip != null)
            {
                shotSource.pitch = Random.Range(0.96f, 1.04f);
                shotSource.PlayOneShot(clip, shotVolume);
            }
        }

        private void LateUpdate()
        {
            speed = Mathf.MoveTowards(speed, agent.isOnNavMesh ? agent.velocity.magnitude : 0f,
                12f * Time.deltaTime);
            stride += speed * 5f * Time.deltaTime;
            if (visualBody != null)
                visualBody.localPosition = bodyRest + Vector3.up * Mathf.Sin(stride) * speed * 0.025f;

            float elapsed = Time.time - shotTime;
            if (recoilPart != null)
                recoilPart.localPosition = recoilRest - Vector3.forward *
                    (0.14f * Mathf.Clamp01(1f - elapsed / 0.16f));
            if (elapsed >= FlashDuration) HideShotEffects();
        }

        private void HideShotEffects()
        {
            if (muzzleFlash != null && muzzleFlash.activeSelf) muzzleFlash.SetActive(false);
            if (tracer != null) tracer.enabled = false;
        }

        private void OnDisable()
        {
            HideShotEffects();
            shotTime = float.NegativeInfinity;
            if (shotSource != null) shotSource.Stop();
        }

        private void OnDestroy()
        {
            if (tracerMaterial != null) Destroy(tracerMaterial);
        }
    }
}
