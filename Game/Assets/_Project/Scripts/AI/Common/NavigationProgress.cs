using UnityEngine;
using UnityEngine.AI;

namespace FacilityEscape.AI.Common
{
    // Measures progress only. Each agent decides how its own architecture recovers.
    public sealed class NavigationProgress
    {
        private Vector3 position;
        private float progressedAt;
        private bool tracking;

        public void Reset() => tracking = false;

        public bool IsBlocked(NavMeshAgent agent, float timeout = 3f)
        {
            if (!agent.enabled || !agent.isOnNavMesh || agent.isStopped)
            {
                Reset();
                return false;
            }
            if (!tracking || (agent.nextPosition - position).sqrMagnitude > 0.01f)
            {
                position = agent.nextPosition;
                progressedAt = Time.time;
                tracking = true;
            }
            if (agent.pathPending) return Time.time - progressedAt >= timeout;
            if (agent.pathStatus != NavMeshPathStatus.PathComplete) return true;
            if (agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                Reset();
                return false;
            }
            return Time.time - progressedAt >= timeout;
        }
    }
}
