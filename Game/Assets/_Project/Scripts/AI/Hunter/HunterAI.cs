using UnityEngine;
using UnityEngine.AI;

namespace FacilityEscape.AI.Hunter
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(HunterPatrol))]
    public sealed class HunterAI : MonoBehaviour
    {
        public enum HunterState { PATROL, CHASE }
        [SerializeField] private CharacterController player;
        [SerializeField] private Transform eyes;
        [SerializeField, Min(0f)] private float detectionRadius = 12f;
        [SerializeField, Range(0f, 360f)] private float fieldOfView = 90f;
        [SerializeField, Min(0f)] private float chaseSpeed = 4f;
        [SerializeField, Min(0f)] private float sightLossDelay = 1.25f;

        private NavMeshAgent agent;
        private HunterPatrol patrol;
        private float patrolSpeed;
        private Vector3 interruptedPatrolDestination;
        private float lastSeenTime;
        private float nextVisionCheck;
        private float nextChaseUpdate;
        private bool playerVisible;

        public HunterState State { get; private set; } = HunterState.PATROL;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            patrol = GetComponent<HunterPatrol>();
            patrolSpeed = agent.speed;
        }

        private void Update()
        {
            if (!agent.isOnNavMesh || player == null || eyes == null) return;

            // Ten vision checks per second are enough for this first version.
            if (Time.time >= nextVisionCheck)
            {
                nextVisionCheck = Time.time + 0.1f;
                playerVisible = CanSeePlayer();
                if (playerVisible)
                {
                    lastSeenTime = Time.time;
                    if (State == HunterState.PATROL) BeginChase();
                }
            }

            if (State != HunterState.CHASE) return;
            if (!playerVisible)
            {
                // Pause while sight is lost; do not follow a hidden player or search.
                agent.isStopped = true;
                if (Time.time - lastSeenTime >= sightLossDelay) ReturnToPatrol();
                return;
            }

            agent.isStopped = false;
            if (Time.time >= nextChaseUpdate)
            {
                nextChaseUpdate = Time.time + 0.2f;
                agent.SetDestination(player.transform.position);
            }
        }

        public bool CanSeePlayer()
        {
            if (player == null || eyes == null || !player.gameObject.activeInHierarchy ||
                Vector3.Distance(transform.position, player.transform.position) > detectionRadius)
                return false;

            Vector3 target = player.transform.TransformPoint(player.center);
            Vector3 direction = target - eyes.position;
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            Vector3 horizontalForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (Vector3.Angle(horizontalForward, horizontalDirection) > fieldOfView * 0.5f)
                return false;

            RaycastHit[] hits = Physics.RaycastAll(eyes.position, direction.normalized,
                direction.magnitude + 0.05f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearest = hit.collider;
                }
            }
            return nearest != null && nearest.transform.IsChildOf(player.transform);
        }

        private void BeginChase()
        {
            interruptedPatrolDestination = agent.destination;
            patrol.enabled = false;
            State = HunterState.CHASE;
            agent.speed = chaseSpeed;
            agent.isStopped = false;
            nextChaseUpdate = Time.time;
            Debug.Log("Hunter state: CHASE", this);
        }

        private void ReturnToPatrol()
        {
            State = HunterState.PATROL;
            agent.speed = patrolSpeed;
            patrol.enabled = true;
            agent.SetDestination(interruptedPatrolDestination);
            agent.isStopped = patrol.IsWaiting;
            Debug.Log("Hunter state: PATROL", this);
        }

        private void OnDisable()
        {
            if (State == HunterState.CHASE && agent != null && agent.enabled &&
                agent.isOnNavMesh && patrol != null)
                ReturnToPatrol();
        }
    }
}
