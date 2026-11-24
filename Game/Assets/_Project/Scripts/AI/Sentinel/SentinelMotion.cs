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

        private NavMeshAgent agent;
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

        public void Fire()
        {
            shotTime = Time.time;
            if (muzzleFlash != null) muzzleFlash.SetActive(true);
            Sfx.Play(SfxId.EnemyShot, MuzzlePosition);
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
            if (muzzleFlash != null && elapsed >= 0.07f && muzzleFlash.activeSelf)
                muzzleFlash.SetActive(false);
        }
    }
}
