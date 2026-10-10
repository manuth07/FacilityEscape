using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using FacilityEscape.AI.Common;

namespace FacilityEscape.AI.Hunter
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(HunterPatrol))]
    public sealed class HunterAI : MonoBehaviour
    {
        public enum HunterState { PATROL, CHASE, SEARCH_LAST_KNOWN, SEARCH_AREA, INVESTIGATE_SOUND }

        [SerializeField] private CharacterController player;
        [SerializeField] private Transform eyes;
        [SerializeField, Min(0f)] private float detectionRadius = 12f;
        [SerializeField, Range(0f, 360f)] private float fieldOfView = 90f;
        [SerializeField, Min(0f)] private float chaseSpeed = 4f;
        [SerializeField, Min(0.7f)] private float chaseStoppingDistance = 1.1f;
        [SerializeField, Min(0f)] private float sightLossDelay = 1.25f;
        [SerializeField, Min(0f)] private float searchDuration = 5f;
        [SerializeField, Min(0.5f)] private float searchRadius = 2f;

        [Header("Sound stimulus scoring")]
        [SerializeField, Min(0f)] private float priorityWeight = 1f;
        [SerializeField, Min(0f)] private float intensityWeight = 2f;
        [SerializeField, Min(0f)] private float distanceWeight = 1f;
        [SerializeField, Min(0f)] private float ageWeight = 1f;
        [SerializeField] private float minimumSoundScore = 0.5f;

        private NavMeshAgent agent;
        private HunterPatrol patrol;
        private float patrolSpeed;
        private float normalStoppingDistance;
        private bool normalUpdateRotation;
        private Vector3 interruptedPatrolDestination;
        private Vector3 lastKnownPlayerPosition;
        private Vector3 soundPosition;
        private Vector3 soundDestination;
        private SoundEvent activeSound;
        private bool hasSoundFocus;
        private const float SoundRepeatDistance = 1f;
        private float lastSeenTime;
        private float nextVisionCheck;
        private float nextChaseUpdate;
        private bool playerVisible;
        private float searchEndsAt;
        private float lookUntil;
        private float lookYaw;
        private int searchPointIndex;
        private readonly List<Vector3> searchPoints = new List<Vector3>();

        public HunterState State { get; private set; } = HunterState.PATROL;
        public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            patrol = GetComponent<HunterPatrol>();
            patrolSpeed = agent.speed;
            normalStoppingDistance = agent.stoppingDistance;
            CapsuleCollider body = GetComponent<CapsuleCollider>();
            if (body != null && player != null)
                Physics.IgnoreCollision(body, player, true);
            normalUpdateRotation = agent.updateRotation;
        }

        private void OnEnable()
        {
            SoundEvents.Emitted += OnSoundHeard;
        }

        private void Update()
        {
            if (!agent.isOnNavMesh || player == null || eyes == null) return;

            if (Time.time >= nextVisionCheck)
            {
                nextVisionCheck = Time.time + 0.1f;
                playerVisible = CanSeePlayer();
                if (playerVisible)
                {
                    // Never update this memory with an unseen player's position.
                    lastKnownPlayerPosition = player.transform.position;
                    lastSeenTime = Time.time;
                    if (State != HunterState.CHASE) ChangeState(HunterState.CHASE);
                }
            }

            switch (State)
            {
                case HunterState.PATROL:
                    // The existing HunterPatrol component still owns waypoint movement.
                    break;
                case HunterState.CHASE:
                    UpdateChase();
                    break;
                case HunterState.SEARCH_LAST_KNOWN:
                    UpdateLastKnownSearch();
                    break;
                case HunterState.SEARCH_AREA:
                    UpdateAreaSearch();
                    break;
                case HunterState.INVESTIGATE_SOUND:
                    UpdateSoundInvestigation();
                    break;
            }
        }

        private void UpdateChase()
        {
            if (!playerVisible)
            {
                agent.isStopped = true;
                if (Time.time - lastSeenTime >= sightLossDelay)
                    ChangeState(HunterState.SEARCH_LAST_KNOWN);
                return;
            }

            Vector3 separation = Vector3.ProjectOnPlane(
                player.transform.position - transform.position, Vector3.up);
            if (separation.sqrMagnitude <= chaseStoppingDistance * chaseStoppingDistance)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                return;
            }

            agent.isStopped = false;
            if (Time.time >= nextChaseUpdate)
            {
                nextChaseUpdate = Time.time + 0.2f;
                agent.SetDestination(lastKnownPlayerPosition);
            }
        }

        private void UpdateLastKnownSearch()
        {
            if (agent.pathPending) return;
            if (agent.pathStatus != NavMeshPathStatus.PathComplete || HasArrived())
                ChangeState(HunterState.SEARCH_AREA);
        }

        private void UpdateAreaSearch()
        {
            if (Time.time >= searchEndsAt)
            {
                ChangeState(HunterState.PATROL);
                return;
            }

            if (agent.isStopped)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.Euler(0f, lookYaw, 0f), 120f * Time.deltaTime);
                if (Time.time >= lookUntil && searchPointIndex < searchPoints.Count)
                    MoveToNextSearchPoint();
                return;
            }

            if (!agent.pathPending &&
                (agent.pathStatus != NavMeshPathStatus.PathComplete || HasArrived()))
            {
                agent.isStopped = true;
                agent.updateRotation = false;
                lookYaw = transform.eulerAngles.y + 120f;
                lookUntil = searchPointIndex < searchPoints.Count
                    ? Time.time + 0.75f : searchEndsAt;
            }
        }

        public float CalculateSoundScore(SoundEvent sound)
        {
            if (sound.HearingRadius <= 0f) return float.NegativeInfinity;
            float normalizedDistance = Mathf.Clamp01(
                Vector3.Distance(transform.position, sound.Position) / sound.HearingRadius);
            float age = Mathf.Max(0f, Time.time - sound.EmittedAt);
            return priorityWeight * sound.Priority + intensityWeight * sound.Intensity
                - distanceWeight * normalizedDistance - ageWeight * age;
        }

        private void OnSoundHeard(SoundEvent sound)
        {
            float score = CalculateSoundScore(sound);
            if (agent == null || !agent.isOnNavMesh || player == null || eyes == null ||
                sound.HearingRadius <= 0f || score < minimumSoundScore ||
                Vector3.SqrMagnitude(transform.position - sound.Position) >
                sound.HearingRadius * sound.HearingRadius || CanSeePlayer())
                return;

            bool followingSound = hasSoundFocus &&
                (State == HunterState.INVESTIGATE_SOUND || State == HunterState.SEARCH_AREA);
            if (followingSound)
            {
                // Re-score the current stimulus using its age and Hunter's current distance.
                float currentScore = CalculateSoundScore(activeSound);
                if (score <= currentScore) return;

                // Fresh nearby shots can refresh the stimulus without restarting its search.
                if ((sound.Position - soundPosition).sqrMagnitude <=
                    SoundRepeatDistance * SoundRepeatDistance)
                {
                    activeSound = sound;
                    return;
                }
            }

            Vector3 destination;
            if (!TryReachablePoint(sound.Position, out destination)) return;
            soundPosition = sound.Position;
            soundDestination = destination;
            activeSound = sound;
            hasSoundFocus = true;
            Debug.Log($"Hunter sound: position={soundPosition}, score={score:F2}, result=chosen", this);
            if (State == HunterState.INVESTIGATE_SOUND)
                agent.SetDestination(soundDestination);
            else
                ChangeState(HunterState.INVESTIGATE_SOUND);
        }

        private void UpdateSoundInvestigation()
        {
            if (agent.pathPending) return;
            if (agent.pathStatus != NavMeshPathStatus.PathComplete || HasArrived())
                ChangeState(HunterState.SEARCH_AREA);
        }

        private bool HasArrived()
        {
            return !agent.pathPending &&
                agent.remainingDistance <= agent.stoppingDistance + 0.1f &&
                agent.velocity.sqrMagnitude <= 0.04f;
        }

        private void ChangeState(HunterState next)
        {
            if (State == next) return;
            HunterState previous = State;
            if (previous == HunterState.PATROL)
            {
                interruptedPatrolDestination = agent.destination;
                patrol.enabled = false;
            }

            State = next;
            if (next == HunterState.PATROL || next == HunterState.CHASE ||
                next == HunterState.SEARCH_LAST_KNOWN)
                hasSoundFocus = false;
            agent.stoppingDistance = next == HunterState.CHASE
                ? chaseStoppingDistance : normalStoppingDistance;
            agent.updateRotation = normalUpdateRotation;
            Debug.Log("Hunter state: " + previous + " -> " + next, this);
            if (next == HunterState.INVESTIGATE_SOUND)
                Debug.Log("Hunter investigating sound", this);
            else if (next == HunterState.PATROL)
                Debug.Log("Hunter resumed patrol", this);

            switch (next)
            {
                case HunterState.PATROL:
                    agent.speed = patrolSpeed;
                    patrol.enabled = true;
                    agent.SetDestination(interruptedPatrolDestination);
                    agent.isStopped = patrol.IsWaiting;
                    break;
                case HunterState.CHASE:
                    agent.speed = chaseSpeed;
                    agent.isStopped = false;
                    nextChaseUpdate = Time.time;
                    break;
                case HunterState.INVESTIGATE_SOUND:
                    agent.speed = patrolSpeed;
                    agent.isStopped = false;
                    agent.SetDestination(soundDestination);
                    break;
                case HunterState.SEARCH_LAST_KNOWN:
                    agent.speed = patrolSpeed;
                    agent.isStopped = false;
                    Vector3 destination;
                    if (!TryReachablePoint(lastKnownPlayerPosition, out destination))
                    {
                        ChangeState(HunterState.SEARCH_AREA);
                        break;
                    }
                    agent.SetDestination(destination);
                    break;
                case HunterState.SEARCH_AREA:
                    agent.speed = patrolSpeed;
                    agent.ResetPath();
                    agent.isStopped = true;
                    agent.updateRotation = false;
                    searchEndsAt = Time.time + searchDuration;
                    lookUntil = Time.time + 0.75f;
                    lookYaw = transform.eulerAngles.y + 120f;
                    ChooseSearchPoints();
                    searchPointIndex = 0;
                    break;
            }
        }

        private bool TryReachablePoint(Vector3 candidate, out Vector3 point)
        {
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
            NavMeshHit hit;
            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(candidate, out hit, 0.75f, filter) &&
                NavMesh.CalculatePath(agent.nextPosition, hit.position, filter, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                point = hit.position;
                return true;
            }
            point = default;
            return false;
        }

        private void ChooseSearchPoints()
        {
            searchPoints.Clear();
            Vector3 center = agent.nextPosition;
            // Try six evenly spaced candidates and keep up to three reachable points.
            for (int i = 0; i < 6 && searchPoints.Count < 3; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 60f, 0f) *
                    Vector3.forward * searchRadius;
                Vector3 point;
                if (!TryReachablePoint(center + offset, out point) ||
                    Vector3.Distance(center, point) < 0.75f || !HasBodyClearance(point))
                    continue;
                bool duplicate = searchPoints.Exists(p => Vector3.Distance(p, point) < 0.75f);
                if (!duplicate) searchPoints.Add(point);
            }
        }

        private bool HasBodyClearance(Vector3 point)
        {
            // Reject points inside solid props, including colliders added after the bake.
            float radius = agent.radius;
            Collider[] overlaps = Physics.OverlapCapsule(
                point + Vector3.up * (radius + 0.05f),
                point + Vector3.up * (agent.height - radius),
                radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider obstacle in overlaps)
                if (!obstacle.transform.IsChildOf(transform)) return false;
            return true;
        }

        private void MoveToNextSearchPoint()
        {
            agent.updateRotation = normalUpdateRotation;
            agent.isStopped = false;
            agent.SetDestination(searchPoints[searchPointIndex++]);
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

        private void OnDisable()
        {
            SoundEvents.Emitted -= OnSoundHeard;
            if (State != HunterState.PATROL && agent != null && agent.enabled &&
                agent.isOnNavMesh && patrol != null)
                ChangeState(HunterState.PATROL);
        }
    }
}
