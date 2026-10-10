using UnityEngine;
using UnityEngine.AI;

namespace FacilityEscape.AI.Hunter
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class HunterPatrol : MonoBehaviour
    {
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField, Min(0f)] private float waitAtPoint = 1f;

        private NavMeshAgent agent;
        private int currentPoint;
        private bool waiting;
        private float resumeTime;

        public int CurrentWaypointIndex => currentPoint;
        public bool IsWaiting => waiting;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            if (!agent.isOnNavMesh || patrolPoints == null || patrolPoints.Length == 0)
            {
                Debug.LogError("Hunter patrol requires a baked NavMesh and assigned patrol points.", this);
                enabled = false;
                return;
            }

            foreach (Transform point in patrolPoints)
            {
                if (point == null)
                {
                    Debug.LogError("Hunter patrol has an unassigned waypoint.", this);
                    enabled = false;
                    return;
                }
            }
            GoToCurrentPoint();
        }

        private void Update()
        {
            if (!agent.isOnNavMesh) return;

            if (waiting)
            {
                if (Time.time < resumeTime) return;
                waiting = false;
                currentPoint = (currentPoint + 1) % patrolPoints.Length;
                GoToCurrentPoint();
                return;
            }

            if (agent.pathPending || agent.pathStatus != NavMeshPathStatus.PathComplete)
                return;

            if (agent.remainingDistance <= agent.stoppingDistance + 0.05f &&
                agent.velocity.sqrMagnitude <= 0.01f)
            {
                agent.isStopped = true;
                waiting = true;
                resumeTime = Time.time + waitAtPoint;
            }
        }

        private void GoToCurrentPoint()
        {
            agent.isStopped = false;
            if (!agent.SetDestination(patrolPoints[currentPoint].position))
            {
                Debug.LogError("Hunter could not set its patrol destination.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
        }
    }
}
