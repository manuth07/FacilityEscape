using UnityEngine;
using UnityEngine.AI;
using FacilityEscape.AI.Common;

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
        private readonly NavigationProgress progress = new NavigationProgress();

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

            if (progress.IsBlocked(agent))
            {
                // Skip this waypoint after a bounded wait; retry it on a later circuit.
                agent.ResetPath();
                agent.isStopped = true;
                waiting = true;
                resumeTime = Time.time + Mathf.Max(1f, waitAtPoint);
                progress.Reset();
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
            progress.Reset();
            agent.isStopped = false;
            if (!agent.SetDestination(patrolPoints[currentPoint].position))
            {
                waiting = true;
                resumeTime = Time.time + Mathf.Max(1f, waitAtPoint);
            }
        }

        private void OnDisable()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
        }
    }
}
