using FacilityEscape.AI.Common;
using FacilityEscape.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityEscape.AI.Repairer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class RepairerAI : MonoBehaviour
    {
        [SerializeField] private Transform safePoint;
        [SerializeField] private Transform[] retreatMarkers;
        [SerializeField, Min(0.5f)] private float repairRange = 1.8f;
        [SerializeField, Min(1)] private int healAmount = 12;
        [SerializeField, Min(0.1f)] private float healInterval = 0.7f;
        [SerializeField, Min(0.1f)] private float threatDuration = 5f;

        private const float DecisionInterval = 0.15f;
        private const float ArrivalDistance = 0.25f;
        private NavMeshPath path;
        private NavMeshAgent agent;
        private Health repairTarget;
        private SoundEvent lastGunfire;
        private float threatUntil;
        private float nextDecision;
        private float nextHeal;
        private bool repairingInRange;

        public float RepairScore { get; private set; }
        public float EscapeScore { get; private set; }
        public float ReturnScore { get; private set; }
        public RepairerAction SelectedAction { get; private set; }
        public string Status { get; private set; }
        public Health ActiveRepairTarget =>
            repairingInRange && isActiveAndEnabled && agent.isOnNavMesh &&
            repairTarget != null && repairTarget.isActiveAndEnabled && !repairTarget.IsDestroyed &&
            repairTarget.CurrentHealth < repairTarget.MaxHealth && InRange(repairTarget, transform.position)
                ? repairTarget
                : null;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
            agent.stoppingDistance = 0f;
        }

        private void OnEnable() => SoundEvents.Emitted += Hear;

        private void OnDisable()
        {
            repairingInRange = false;
            SoundEvents.Emitted -= Hear;
            if (agent.isOnNavMesh) agent.ResetPath();
        }

        private void Hear(SoundEvent sound)
        {
            if (!sound.IsGunfire || Vector3.Distance(transform.position, sound.Position) > sound.HearingRadius)
                return;

            lastGunfire = sound;
            threatUntil = Time.time + threatDuration;
        }

        private void Update()
        {
            if (Time.time < nextDecision) return;
            nextDecision = Time.time + DecisionInterval;
            Decide();
        }

        private void Decide()
        {
            repairingInRange = false;
            RepairScore = EscapeScore = ReturnScore = 0f;
            Status = null;
            if (!agent.isOnNavMesh)
            {
                SelectedAction = RepairerAction.Hold;
                Status = "Repairer is off the NavMesh";
                return;
            }

            Health ally = FindRepairTarget(out Vector3 repairPosition);
            bool threatened = Time.time < threatUntil;
            Vector3 retreat = default;
            Vector3 home = default;
            if (threatened)
            {
                float proximity = 1f - Vector3.Distance(transform.position, lastGunfire.Position) /
                    lastGunfire.HearingRadius;
                if (FindRetreat(out retreat))
                    EscapeScore = RepairerScoring.Escape(proximity, (threatUntil - Time.time) / threatDuration);
                else
                    Status = "No reachable retreat marker";
            }
            else if (ally == null && safePoint != null &&
                     TryPath(safePoint.position, out home, out float homeDistance) &&
                     homeDistance > ArrivalDistance)
            {
                ReturnScore = RepairerScoring.Return(homeDistance);
            }

            if (threatened && EscapeScore == 0f) RepairScore = 0f;
            SelectedAction = RepairerScoring.Select(RepairScore, EscapeScore, ReturnScore);
            switch (SelectedAction)
            {
                case RepairerAction.Repair:
                    if (repairTarget != ally) nextHeal = Time.time + healInterval;
                    repairTarget = ally;
                    if (InRange(ally, transform.position))
                    {
                        repairingInRange = true;
                        agent.ResetPath();
                        if (Time.time >= nextHeal && ally.Heal(healAmount))
                            nextHeal = Time.time + healInterval;
                    }
                    else
                    {
                        MoveTo(repairPosition);
                        nextHeal = Time.time + healInterval;
                    }
                    return;
                case RepairerAction.Escape:
                    MoveTo(retreat);
                    break;
                case RepairerAction.Return:
                    MoveTo(home);
                    break;
                default:
                    agent.ResetPath();
                    break;
            }
            repairTarget = null;
        }

        private Health FindRepairTarget(out Vector3 destination)
        {
            Health best = null;
            destination = default;
            bool unreachable = false;
            foreach (var marker in FindObjectsByType<RepairableAlly>(FindObjectsSortMode.None))
            {
                Health ally = marker.Health;
                if (!marker.isActiveAndEnabled || ally == null || ally.gameObject == gameObject ||
                    !ally.isActiveAndEnabled || ally.IsDestroyed || ally.CurrentHealth >= ally.MaxHealth)
                    continue;

                if (!FindApproach(ally, out Vector3 approach, out float length))
                {
                    unreachable = true;
                    continue;
                }

                float missing = 1f - (float)ally.CurrentHealth / ally.MaxHealth;
                float score = RepairerScoring.Repair(missing, length);
                if (score <= RepairScore) continue;
                best = ally;
                destination = approach;
                RepairScore = score;
            }

            if (best == null)
                Status = unreachable ? "No complete path into repair range" : "No damaged ally";
            return best;
        }

        private bool FindApproach(Health ally, out Vector3 destination, out float length)
        {
            destination = agent.nextPosition;
            length = 0f;
            if (InRange(ally, destination)) return true;

            length = float.PositiveInfinity;
            Vector3 center = ally.transform.position;
            float approachRange = repairRange - ArrivalDistance;
            for (int i = 0; i <= 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                Vector3 offset = i == 8 ? Vector3.zero :
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (approachRange - 0.05f);
                if (TryPath(center + offset, out Vector3 point, out float distance) &&
                    (center - point).sqrMagnitude <= approachRange * approachRange &&
                    distance < length)
                {
                    destination = point;
                    length = distance;
                }
            }
            return !float.IsPositiveInfinity(length);
        }

        private bool FindRetreat(out Vector3 destination)
        {
            destination = default;
            float bestDistance = -1f;
            if (retreatMarkers == null) return false;
            foreach (Transform marker in retreatMarkers)
            {
                if (marker == null || !TryPath(marker.position, out Vector3 point, out _)) continue;
                float distance = (point - lastGunfire.Position).sqrMagnitude;
                if (distance <= bestDistance) continue;
                bestDistance = distance;
                destination = point;
            }
            return bestDistance >= 0f;
        }

        private bool TryPath(Vector3 position, out Vector3 destination, out float length)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            destination = default;
            length = 0f;
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 0.5f, filter) ||
                !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;

            destination = hit.position;
            Vector3 previous = agent.nextPosition;
            foreach (Vector3 corner in path.corners)
            {
                length += Vector3.Distance(previous, corner);
                previous = corner;
            }
            return true;
        }

        private bool InRange(Health ally, Vector3 point) =>
            (ally.transform.position - point).sqrMagnitude <= repairRange * repairRange;

        private void MoveTo(Vector3 destination)
        {
            if ((agent.nextPosition - destination).sqrMagnitude <= ArrivalDistance * ArrivalDistance)
                agent.ResetPath();
            else if (!agent.hasPath || (agent.destination - destination).sqrMagnitude > 0.01f)
                agent.SetDestination(destination);
        }
    }
}
