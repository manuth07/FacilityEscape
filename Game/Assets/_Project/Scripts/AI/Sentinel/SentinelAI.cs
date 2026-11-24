using FacilityEscape.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityEscape.AI.Sentinel
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(SentinelMotion))]
    public sealed class SentinelAI : MonoBehaviour
    {
        [SerializeField] private CharacterController player;
        [SerializeField] private Transform guardPoint;
        [SerializeField] private Transform[] coverPoints;
        [SerializeField] private LayerMask sightMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(1)] private float guardRadius = 12f;
        [SerializeField, Min(1)] private float sightRange = 14f;
        [SerializeField, Range(1, 180)] private float halfViewAngle = 75f;
        [SerializeField, Min(0.1f)] private float memorySeconds = 5f;
        [SerializeField, Min(0.1f)] private float hitResponseSeconds = 2f;
        [SerializeField, Min(0.1f)] private float shotInterval = 1f;
        [SerializeField, Min(0f)] private float shotWindup = 0.35f;
        [SerializeField, Min(1)] private int shotDamage = 8;
        [SerializeField] private string activeBranch;

        private enum Status { Failure, Running, Success }

        private NavMeshAgent agent;
        private SentinelMotion motion;
        private Health health;
        private Health playerHealth;
        private NavMeshPath candidatePath;
        private readonly RaycastHit[] sightHits = new RaycastHit[16];
        private Vector3 spawnPosition;
        private Vector3 spawnFacing;
        private Vector3 lastThreat;
        private Vector3 target;
        private float seenUntil = float.NegativeInfinity;
        private float hitUntil = float.NegativeInfinity;
        private float nextShot;
        private float aimStartedAt = float.NegativeInfinity;
        private int previousHealth;
        private bool visible;
        private Transform occupiedCover;
        private Transform movingCover;
        private Vector3 coverDestination;
        private Vector3? requestedDestination;
        private bool relocating;

        private Vector3 GuardPosition => guardPoint != null ? guardPoint.position : spawnPosition;
        private bool RemembersThreat => Time.time < seenUntil;
        private static readonly Vector3 ChestOffset = Vector3.up * 1.1f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            motion = GetComponent<SentinelMotion>();
            candidatePath = new NavMeshPath();
            spawnPosition = transform.position;
            spawnFacing = transform.forward;
        }

        private void Start()
        {
            previousHealth = health.CurrentHealth;
            if (player == null)
            {
                GameObject taggedPlayer = GameObject.FindWithTag("Player");
                if (taggedPlayer != null)
                    player = taggedPlayer.GetComponentInChildren<CharacterController>();
            }
            if (player != null)
                playerHealth = player.GetComponentInParent<Health>();
        }

        private void Update()
        {
            if (!agent.isOnNavMesh)
            {
                CancelAim();
                activeBranch = "Waiting for NavMesh";
                return;
            }

            Sense();
            if (!visible) CancelAim();
            // Priority selector. A successful or running child owns movement for this frame.
            if (TickCover(true) != Status.Failure) return;
            if (TickCover(false) != Status.Failure) return;
            if (TickAttack() != Status.Failure) return;
            if (TickHold() != Status.Failure) return;
            TickGuard();
        }

        private void Sense()
        {
            bool hit = health.CurrentHealth < previousHealth;
            previousHealth = health.CurrentHealth;
            if (hit) hitUntil = Time.time + hitResponseSeconds;
            visible = false;
            if (player == null || !player.gameObject.activeInHierarchy || playerHealth == null ||
                playerHealth.IsDestroyed)
                return;

            target = player.bounds.center;
            Vector3 offset = target - motion.EyePosition;
            Vector3 flatOffset = Vector3.ProjectOnPlane(offset, Vector3.up);
            bool clear = offset.sqrMagnitude <= sightRange * sightRange &&
                !IsBlocked(motion.EyePosition, target);
            bool facing = Vector3.Angle(transform.forward, flatOffset) <= halfViewAngle;
            // A visible attacker or a threat exposing occupied cover can be noticed from the side.
            visible = clear && (facing || hit || (occupiedCover != null && RemembersThreat));
            if (visible)
            {
                lastThreat = target;
                seenUntil = Time.time + memorySeconds;
            }
        }

        private Status TickCover(bool relocate)
        {
            if (!RemembersThreat) return Status.Failure;

            if (movingCover != null && relocating == relocate &&
                IsProtected(coverDestination) && (agent.pathPending ||
                    agent.pathStatus == NavMeshPathStatus.PathComplete))
                return AdvanceCover();

            if (movingCover != null && relocating == relocate)
            {
                ClearCoverMove();
                Stop();
            }

            if (occupiedCover != null)
            {
                if (!relocate || IsProtected(transform.position)) return Status.Failure;
                if (!visible && IsProtected(occupiedCover.position)) return Status.Failure;
            }
            else if (relocate || Time.time >= hitUntil || relocating)
                return Status.Failure;

            if (!FindCover(out Transform marker, out Vector3 destination))
                return Status.Failure;

            if (!Drive(destination)) return Status.Failure;
            movingCover = marker;
            coverDestination = destination;
            relocating = relocate;
            return AdvanceCover();
        }

        private Status AdvanceCover()
        {
            CancelAim();
            activeBranch = relocating ? "Relocate" : "Seek cover";
            if (!Drive(coverDestination))
            {
                ClearCoverMove();
                return Status.Failure;
            }

            if (!Arrived(coverDestination))
            {
                motion.Face(agent.desiredVelocity);
                return Status.Running;
            }

            occupiedCover = movingCover;
            ClearCoverMove();
            Stop();
            motion.Face(lastThreat - transform.position);
            return Status.Success;
        }

        private void ClearCoverMove()
        {
            movingCover = null;
            relocating = false;
        }

        private bool FindCover(out Transform marker, out Vector3 destination)
        {
            marker = null;
            destination = default;
            float shortest = float.PositiveInfinity;
            if (coverPoints == null) return false;

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            foreach (Transform point in coverPoints)
            {
                if (point == null || point == occupiedCover ||
                    !NavMesh.SamplePosition(point.position, out NavMeshHit hit, 0.75f, filter) ||
                    !IsProtected(hit.position) ||
                    !agent.CalculatePath(hit.position, candidatePath) ||
                    candidatePath.status != NavMeshPathStatus.PathComplete)
                    continue;

                Vector3[] corners = candidatePath.corners;
                if (corners.Length == 0) continue;

                float length = 0f;
                Vector3 previous = transform.position;
                bool withinGuardArea = true;
                foreach (Vector3 corner in corners)
                {
                    withinGuardArea &= (corner - GuardPosition).sqrMagnitude <= guardRadius * guardRadius;
                    length += Vector3.Distance(previous, corner);
                    previous = corner;
                }

                if (!withinGuardArea || length >= shortest) continue;
                shortest = length;
                marker = point;
                destination = hit.position;
            }
            return marker != null;
        }

        private bool IsProtected(Vector3 position)
        {
            return (position - GuardPosition).sqrMagnitude <= guardRadius * guardRadius &&
                IsBlocked(lastThreat, position + ChestOffset);
        }

        private bool IsBlocked(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            float distance = offset.magnitude;
            if (distance < 0.001f) return false;

            Vector3 direction = offset / distance;
            int count = Physics.RaycastNonAlloc(from, direction, sightHits, distance, sightMask,
                QueryTriggerInteraction.Ignore);

            if (HasObstruction(sightHits, count)) return true;
            if (count < sightHits.Length) return false;

            // A saturated query may have omitted a wall behind ignored player or robot colliders.
            RaycastHit[] allHits = Physics.RaycastAll(from, direction, distance, sightMask,
                QueryTriggerInteraction.Ignore);
            return HasObstruction(allHits, allHits.Length);
        }

        private bool HasObstruction(RaycastHit[] hits, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Transform body = hits[i].collider.transform;
                if (body.IsChildOf(transform) ||
                    (playerHealth != null && body.IsChildOf(playerHealth.transform)))
                    continue;
                return true;
            }
            return false;
        }

        private Status TickAttack()
        {
            if (!visible) return Status.Failure;
            activeBranch = "Attack";
            ClearCoverMove();
            Stop();
            Vector3 aim = target - transform.position;
            motion.Face(aim);
            if (Time.time < nextShot ||
                Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(aim, Vector3.up)) > 8f ||
                IsBlocked(motion.MuzzlePosition, target))
            {
                CancelAim();
                return Status.Running;
            }

            if (float.IsNegativeInfinity(aimStartedAt))
            {
                aimStartedAt = Time.time;
                if (shotWindup > 0f) motion.BeginAim();
            }
            if (Time.time < aimStartedAt + shotWindup) return Status.Running;

            CancelAim();
            if (playerHealth.TakeDamage(shotDamage))
            {
                nextShot = Time.time + shotInterval;
                motion.Fire(target);
            }
            return Status.Running;
        }

        private void CancelAim()
        {
            aimStartedAt = float.NegativeInfinity;
        }

        private Status TickHold()
        {
            if (occupiedCover == null || !RemembersThreat || !IsProtected(transform.position))
                return Status.Failure;
            activeBranch = "Hold cover";
            Stop();
            motion.Face(lastThreat - transform.position);
            return Status.Running;
        }

        private void TickGuard()
        {
            activeBranch = "Guard";
            ClearCoverMove();
            occupiedCover = null;
            if (!Arrived(GuardPosition) && Drive(GuardPosition))
                motion.Face(agent.desiredVelocity);
            else
            {
                Stop();
                motion.Face(guardPoint != null ? guardPoint.forward : spawnFacing);
            }
        }

        private bool Drive(Vector3 destination)
        {
            if (requestedDestination == destination && (agent.pathPending ||
                (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete) ||
                Arrived(destination)))
                return true;
            if (!agent.SetDestination(destination)) return false;
            agent.isStopped = false;
            requestedDestination = destination;
            return true;
        }

        private bool Arrived(Vector3 destination)
        {
            if (agent.pathPending) return false;

            float tolerance = agent.stoppingDistance + 0.05f;
            if (agent.hasPath && requestedDestination == destination)
                return agent.pathStatus == NavMeshPathStatus.PathComplete &&
                    agent.remainingDistance <= tolerance;

            return (destination - transform.position).sqrMagnitude <= tolerance * tolerance;
        }

        private void Stop()
        {
            if (!agent.isStopped || agent.hasPath) agent.ResetPath();
            agent.isStopped = true;
            requestedDestination = null;
        }

        private void OnDisable()
        {
            CancelAim();
            if (agent != null && agent.isOnNavMesh) Stop();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Application.isPlaying ? GuardPosition :
                (guardPoint != null ? guardPoint.position : transform.position), guardRadius);
            if (coverPoints != null)
                foreach (Transform point in coverPoints)
                {
                    if (point == null) continue;
                    Gizmos.color = point == occupiedCover ? Color.green :
                        point == movingCover ? Color.cyan : Color.yellow;
                    Gizmos.DrawWireSphere(point.position, 0.3f);
                }

            if (requestedDestination.HasValue)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(requestedDestination.Value, 0.18f);
                if (agent != null && agent.isOnNavMesh && agent.hasPath)
                {
                    Vector3 previous = transform.position;
                    foreach (Vector3 corner in agent.path.corners)
                    {
                        Gizmos.DrawLine(previous, corner);
                        previous = corner;
                    }
                }
            }
            if (Application.isPlaying && motion != null && RemembersThreat)
            {
                Gizmos.color = visible ? Color.red : new Color(1f, 0.55f, 0.12f);
                Gizmos.DrawLine(motion.EyePosition, lastThreat);
                Gizmos.DrawWireSphere(lastThreat, 0.15f);
            }
        }
    }
}
