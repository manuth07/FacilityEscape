using FacilityEscape.AI.Common;
using FacilityEscape.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityEscape.AI.Repairer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public sealed class RepairerAI : MonoBehaviour
    {
        [SerializeField] private Transform safePoint;
        [SerializeField] private Transform[] retreatMarkers;
        [SerializeField, Min(0.5f)] private float repairRange = 1.8f;
        [SerializeField, Min(1)] private int healAmount = 10;
        [SerializeField, Min(0.1f)] private float healInterval = 1f;
        [SerializeField, Min(0.1f)] private float threatDuration = 5f;
        [SerializeField] private CharacterController playerCollider;
        [SerializeField] private bool showDiagnostics;

        [SerializeField, Range(0f, 0.25f)] private float targetSwitchMargin = 0.07f;

        private const float DecisionInterval = 0.15f;
        private const float ArrivalDistance = 0.25f;
        private NavMeshPath path;
        private NavMeshAgent agent;
        private Health repairTarget;
        private SoundEvent lastGunfire;
        private float threatUntil;
        private float nextDecision;
        private float nextHeal;
        private Health health;
        private readonly Vector3[] corners = new Vector3[64];
        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        private readonly NavigationProgress progress = new NavigationProgress();
        private Health blockedAlly;
        private float retryAllyAt;

        public float RepairScore { get; private set; }
        public float EscapeScore { get; private set; }
        public float ReturnScore { get; private set; }
        public RepairerAction SelectedAction { get; private set; }
        public string Status { get; private set; }
        public float HoldScore => 0f;
        public Health ActiveRepairTarget => agent.enabled && agent.isOnNavMesh && CanRepairCurrentTarget &&
            repairTarget.CurrentHealth < repairTarget.MaxHealth ? repairTarget : null;
        public Health RepairTarget => repairTarget;
        public float LastHealTime { get; private set; } = float.NegativeInfinity;
        public float ThreatSeconds => Mathf.Max(0f, threatUntil - Time.time);
        public float ThreatLevel => Time.time < threatUntil
            ? Mathf.Clamp01((threatUntil - Time.time) / threatDuration) : 0f;
        public bool CanRepairCurrentTarget => isActiveAndEnabled && health != null &&
            !health.IsDestroyed && SelectedAction == RepairerAction.Repair &&
            ValidAlly(repairTarget) && InRange(repairTarget, transform.position) &&
            HasRepairSight(repairTarget, transform.position);

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            path = new NavMeshPath();
            agent.stoppingDistance = 0f;
            if (playerCollider != null)
                foreach (Collider body in GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(body, playerCollider, true);
        }

        private void OnEnable() => SoundEvents.Emitted += Hear;

        private void OnDisable()
        {
            SoundEvents.Emitted -= Hear;
            repairTarget = null;
            LastHealTime = float.NegativeInfinity;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        private void Hear(SoundEvent sound)
        {
            if (!isActiveAndEnabled || health == null || health.IsDestroyed ||
                !sound.IsGunfire || sound.HearingRadius <= 0f ||
                Vector3.Distance(transform.position, sound.Position) > sound.HearingRadius)
                return;

            lastGunfire = sound;
            threatUntil = Time.time + threatDuration;
        }

        private void Update()
        {
            if (health == null || health.IsDestroyed) return;
            if (agent.enabled && agent.isOnNavMesh &&
                (agent.hasPath && agent.isPathStale || progress.IsBlocked(agent)))
            {
                blockedAlly = repairTarget;
                retryAllyAt = Time.time + 0.75f;
                repairTarget = null;
                SelectedAction = RepairerAction.Hold;
                Status = "Movement blocked; reevaluating";
                agent.ResetPath();
                progress.Reset();
                nextDecision = Time.time + 0.5f;
            }
            if (Time.time < nextDecision) return;
            nextDecision = Time.time + DecisionInterval;
            Decide();
        }

        private void Decide()
        {
            RepairScore = EscapeScore = ReturnScore = 0f;
            Status = null;
            if (!agent.enabled || !agent.isOnNavMesh)
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
                    if (InRange(ally, transform.position) && HasRepairSight(ally, transform.position))
                    {
                        agent.ResetPath();
                        if (Time.time >= nextHeal && ally.Heal(healAmount))
                        {
                            LastHealTime = Time.time;
                            nextHeal = Time.time + healInterval;
                        }
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
            float incumbentScore = -1f;
            Vector3 incumbentPosition = default;
            foreach (var marker in RepairableAlly.Registered)
            {
                if (marker == null) continue;
                Health ally = marker.Health;
                if (!marker.isActiveAndEnabled || !ValidAlly(ally) || ally.CurrentHealth >= ally.MaxHealth)
                    continue;
                if (ally == blockedAlly && Time.time < retryAllyAt)
                {
                    unreachable = true;
                    continue;
                }

                if (!FindApproach(ally, out Vector3 approach, out float length))
                {
                    unreachable = true;
                    continue;
                }

                float missing = 1f - (float)ally.CurrentHealth / ally.MaxHealth;
                float score = RepairerScoring.Repair(missing, length);
                if (ally == repairTarget)
                {
                    incumbentScore = score;
                    incumbentPosition = approach;
                }
                if (score <= RepairScore) continue;
                best = ally;
                destination = approach;
                RepairScore = score;
            }

            if (incumbentScore >= 0f && RepairScore <= incumbentScore + targetSwitchMargin)
            {
                destination = incumbentPosition;
                RepairScore = incumbentScore;
                return repairTarget;
            }
            if (best == null)
                Status = unreachable ? "No reachable, unobstructed repair position" : "No damaged ally";
            return best;
        }

        private bool FindApproach(Health ally, out Vector3 destination, out float length)
        {
            destination = agent.nextPosition;
            length = 0f;
            if (InRange(ally, destination) && HasRepairSight(ally, destination)) return true;

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
                    HasRepairSight(ally, point) && HasBodyClearance(point) &&
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
            float currentSeparation = Vector3.Distance(agent.nextPosition, lastGunfire.Position);
            float bestScore = -1f;
            bool found = false;
            if (retreatMarkers == null) return false;
            foreach (Transform marker in retreatMarkers)
            {
                if (marker == null || !marker.gameObject.activeInHierarchy ||
                    !TryPath(marker.position, out Vector3 point, out float travel)) continue;
                float separation = Vector3.Distance(point, lastGunfire.Position);
                if (separation <= currentSeparation + 0.1f) continue;
                Vector3 eye = point + Vector3.up * 0.8f;
                Vector3 source = lastGunfire.Position + Vector3.up * 0.8f;
                bool covered = Physics.Linecast(eye, source, out var hit, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform) &&
                    hit.collider.GetComponentInParent<CharacterController>() == null;
                float score = RepairerScoring.RetreatMarker(separation, travel, covered);
                if (score <= bestScore) continue;
                bestScore = score;
                destination = point;
                found = true;
            }
            return found;
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
            int count = path.GetCornersNonAlloc(corners);
            Vector3[] points = count == corners.Length ? path.corners : corners;
            if (points != corners) count = points.Length;
            for (int i = 0; i < count; i++)
            {
                Vector3 corner = points[i];
                length += Vector3.Distance(previous, corner);
                previous = corner;
            }
            return true;
        }

        private bool InRange(Health ally, Vector3 point) =>
            (ally.transform.position - point).sqrMagnitude <= repairRange * repairRange;

        private bool ValidAlly(Health ally) => ally != null && ally.gameObject != gameObject &&
            ally.isActiveAndEnabled && !ally.IsDestroyed;

        private bool HasBodyClearance(Vector3 point)
        {
            foreach (Collider body in Physics.OverlapCapsule(
                point + Vector3.up * (agent.radius + 0.05f),
                point + Vector3.up * (agent.height - agent.radius), agent.radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!body.transform.IsChildOf(transform)) return false;
            return true;
        }

        private bool HasRepairSight(Health ally, Vector3 point)
        {
            if (!ValidAlly(ally)) return false;
            Vector3 origin = point + Vector3.up * 0.65f;
            Vector3 direction = ally.transform.position + Vector3.up * 0.9f - origin;
            int count = Physics.RaycastNonAlloc(origin, direction.normalized, sightHits,
                direction.magnitude + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = count == sightHits.Length
                ? Physics.RaycastAll(origin, direction.normalized, direction.magnitude + 0.05f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) : sightHits;
            if (hits != sightHits) count = hits.Length;
            Collider nearest = null;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance >= distance) continue;
                nearest = hits[i].collider;
                distance = hits[i].distance;
            }
            return nearest != null && nearest.GetComponentInParent<Health>() == ally;
        }

        private void MoveTo(Vector3 destination)
        {
            agent.isStopped = false;
            if ((agent.nextPosition - destination).sqrMagnitude <= ArrivalDistance * ArrivalDistance)
                agent.ResetPath();
            else if (!agent.hasPath || agent.isPathStale || agent.pathStatus != NavMeshPathStatus.PathComplete ||
                     (agent.destination - destination).sqrMagnitude > 0.01f)
                agent.SetDestination(destination);
        }

        private void OnGUI()
        {
            if (!showDiagnostics) return;
            GUILayout.BeginArea(new Rect(12, 200, 380, 170), GUI.skin.box);
            GUILayout.Label("Repairer: " + SelectedAction + " | Target: " +
                (repairTarget == null ? "none" : repairTarget.name));
            GUILayout.Label($"Hold {HoldScore:F2} | Repair {RepairScore:F2} | Escape {EscapeScore:F2} | Return {ReturnScore:F2}");
            GUILayout.Label($"Threat {ThreatLevel:F2} | Destination {agent.destination}");
            if (!string.IsNullOrEmpty(Status)) GUILayout.Label(Status);
            GUILayout.EndArea();
        }
    }
}
