using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using FacilityEscape.AI.Common;

namespace FacilityEscape.AI.Flanker
{
    /// <summary>
    /// Utility-AI flanker. When alerted (by sight or gunfire) it scores candidate positions
    /// around the player by route length, line of sight, route exposure and flank angle,
    /// then walks to the best one and re-plans as the player and the level change.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class FlankerAI : MonoBehaviour
    {
        public enum FlankerState { IDLE, FLANK, ENGAGE, RETURN }

        [Header("References")]
        [SerializeField] private CharacterController player;
        [SerializeField] private Transform playerEyes;

        [Header("Senses")]
        [SerializeField, Min(0f)] private float detectionRadius = 18f;
        [SerializeField, Min(0f)] private float eyeHeight = 1.5f;
        [SerializeField, Min(0.05f)] private float senseInterval = 0.2f;
        [SerializeField] private int minimumSoundPriority = 5;
        [SerializeField, Min(0f)] private float alertDuration = 12f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.5f;
        [SerializeField, Min(0f)] private float arriveDistance = 0.6f;
        [SerializeField, Min(0f)] private float engageHoldSeconds = 2.5f;
        [SerializeField, Min(1f)] private float turnSpeed = 180f;

        [Header("Candidate generation")]
        [SerializeField, Min(2)] private int anglesPerRing = 16;
        [SerializeField] private float[] ringRadii = { 5f, 8f };
        [SerializeField, Min(0f)] private float minimumPlayerDistance = 3f;
        [SerializeField, Min(0.1f)] private float sampleDistance = 2f;
        [SerializeField, Min(1)] private int candidatesPerFrame = 4;
        [SerializeField, Min(0.1f)] private float reevaluateInterval = 1f;
        [SerializeField, Min(0f)] private float switchMargin = 0.08f;

        [Header("Scoring")]
        [SerializeField] private FlankWeights weights = new FlankWeights();
        [SerializeField, Range(10f, 90f)] private float playerViewHalfAngle = 60f;
        [SerializeField, Min(1f)] private float exposureRange = 25f;
        [SerializeField, Min(0.5f)] private float exposureSampleSpacing = 2.5f;
        [Header("Reaction to damage")]
        [SerializeField, Min(0f)] private float cautiousDuration = 6f;
        [SerializeField, Min(1f)] private float cautiousExposureMultiplier = 2f;

        private float cautiousUntil = -999f;
        private readonly FlankWeights activeWeights = new FlankWeights();

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private bool logDecisions = true;

        private NavMeshAgent agent;
        private NavMeshPath path;
        private readonly Vector3[] cornerBuffer = new Vector3[64];
        private readonly List<FlankCandidate> working = new List<FlankCandidate>();
        private readonly List<FlankCandidate> lastCandidates = new List<FlankCandidate>();

        private FlankCandidate best;
        private bool hasBest;
        private Vector3 homePosition;
        private Vector3 flankReference;
        private Vector3 targetPosition;
        private bool hasTarget;
        private bool isEvaluating;
        private float nextEvaluationTime;
        private float nextSenseTime;
        private float lastStimulusTime = -999f;
        private float engageEndsAt;
        private string lastReason = "";

        public FlankerState State { get; private set; } = FlankerState.IDLE;
        public event Action<FlankerAI> Engaged;
        public CharacterController Player => player;

        private Vector3 PlayerEyePosition =>
            playerEyes != null ? playerEyes.position : player.transform.position + Vector3.up * 1.6f;

        private Vector3 PlayerLookDirection =>
            playerEyes != null ? playerEyes.forward : player.transform.forward;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
            agent.speed = moveSpeed;
        }

        private void OnEnable()
        {
            isEvaluating = false;
            SoundEvents.Emitted += OnSoundHeard;
        }

        private void OnDisable()
        {
            SoundEvents.Emitted -= OnSoundHeard;
        }

        private void Start()
        {
            if (player == null)
            {
                Debug.LogError("FlankerAI needs the player CharacterController assigned.", this);
                enabled = false;
                return;
            }

            if (!agent.isOnNavMesh)
            {
                Debug.LogError("The Flanker must start on the baked NavMesh.", this);
                enabled = false;
                return;
            }

            CapsuleCollider body = GetComponent<CapsuleCollider>();
            if (body != null) Physics.IgnoreCollision(body, player, true);

            homePosition = transform.position;
            agent.isStopped = true;
        }

        private void Update()
        {
            if (player == null || !agent.isOnNavMesh) return;

            if (Time.time >= nextSenseTime)
            {
                nextSenseTime = Time.time + senseInterval;
                if (CanSeePlayer()) Alert("saw the player");
            }

            if ((State == FlankerState.FLANK || State == FlankerState.ENGAGE) &&
                Time.time - lastStimulusTime > alertDuration)
            {
                lastReason = "alert expired";
                ChangeState(FlankerState.RETURN);
            }

            switch (State)
            {
                case FlankerState.FLANK: UpdateFlank(); break;
                case FlankerState.ENGAGE: UpdateEngage(); break;
                case FlankerState.RETURN: UpdateReturn(); break;
            }
        }

        // ---------------------------------------------------------------- senses

        private void OnSoundHeard(SoundEvent sound)
        {
            if (!isActiveAndEnabled) return;
            if (sound.Priority < minimumSoundPriority) return;
            if (Vector3.Distance(transform.position, sound.Position) > sound.HearingRadius) return;
            Alert("heard gunfire");
        }

        private bool CanSeePlayer()
        {
            Vector3 from = transform.position + Vector3.up * eyeHeight;
            Vector3 to = PlayerEyePosition;
            if ((to - from).sqrMagnitude > detectionRadius * detectionRadius) return false;

            if (!Physics.Linecast(from, to, out RaycastHit hit, obstructionMask,
                                  QueryTriggerInteraction.Ignore))
                return true;

            return hit.collider.transform.IsChildOf(player.transform);
        }

        private void Alert(string reason)
        {
            lastStimulusTime = Time.time;
            lastReason = reason;
            if (State == FlankerState.IDLE || State == FlankerState.RETURN)
                BeginFlank();
        }

        // ---------------------------------------------------------------- states

        /// <summary>
        /// Called when the Flanker takes damage. It switches to a different side and, for a while,
        /// weights exposure more heavily so it stays out of the player's line of fire.
        /// </summary>
        public void NotifyDamaged()
        {
            lastStimulusTime = Time.time;
            lastReason = "took damage";
            cautiousUntil = Time.time + cautiousDuration;

            if (logDecisions)
                Debug.Log("Flanker hit: switching side and favouring cover", this);

            if (State == FlankerState.FLANK)
            {
                flankReference = transform.position;
                hasTarget = false;
                nextEvaluationTime = 0f;
            }
            else
            {
                BeginFlank();
            }
        }

        private void RefreshActiveWeights()
        {
            bool cautious = Time.time < cautiousUntil;
            activeWeights.route = weights.route;
            activeWeights.visibility = weights.visibility;
            activeWeights.exposure = weights.exposure * (cautious ? cautiousExposureMultiplier : 1f);
            activeWeights.flank = weights.flank * (cautious ? 2f : 1f);
            activeWeights.maxRouteLength = weights.maxRouteLength;
        }
        private void BeginFlank()
        {
            flankReference = transform.position;
            hasTarget = false;
            nextEvaluationTime = 0f;
            ChangeState(FlankerState.FLANK);
        }

        private void ChangeState(FlankerState next)
        {
            if (State == next) return;
            State = next;
            if (logDecisions) Debug.Log($"Flanker -> {next} ({lastReason})", this);

            switch (next)
            {
                case FlankerState.IDLE:
                    hasBest = false;
                    hasTarget = false;
                    agent.isStopped = true;
                    break;

                case FlankerState.ENGAGE:
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    engageEndsAt = Time.time + engageHoldSeconds;
                    Engaged?.Invoke(this);
                    break;

                case FlankerState.RETURN:
                    hasBest = false;
                    hasTarget = false;
                    agent.isStopped = false;
                    agent.SetDestination(homePosition);
                    break;
            }
        }

        private void UpdateFlank()
        {
            if (hasTarget && !agent.pathPending)
            {
                bool pathBroken = agent.pathStatus != NavMeshPathStatus.PathComplete;
                bool arrived = agent.remainingDistance <= Mathf.Max(arriveDistance, agent.stoppingDistance);

                if (arrived && !pathBroken)
                {
                    lastReason = "arrived at flank position";
                    ChangeState(FlankerState.ENGAGE);
                    return;
                }

                if (pathBroken)
                {
                    // The route was cut (for example a crate was pushed onto it): re-plan now.
                    hasTarget = false;
                    nextEvaluationTime = 0f;
                }
            }

            if (!isEvaluating && Time.time >= nextEvaluationTime)
                StartCoroutine(EvaluateCandidates());
        }

        private void UpdateEngage()
        {
            Vector3 flat = player.transform.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
            {
                Quaternion want = Quaternion.LookRotation(flat);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want,
                                                              turnSpeed * Time.deltaTime);
            }

            // After holding, flank again, preferring a different side from where we are now.
            if (Time.time >= engageEndsAt)
            {
                lastReason = "engage hold finished, switching side";
                BeginFlank();
            }
        }

        private void UpdateReturn()
        {
            if (agent.pathPending) return;
            if (agent.remainingDistance <= Mathf.Max(arriveDistance, agent.stoppingDistance))
                ChangeState(FlankerState.IDLE);
        }

        // ------------------------------------------------- candidate generation

        private IEnumerator EvaluateCandidates()
        {
            isEvaluating = true;
            working.Clear();
            RefreshActiveWeights();

            Vector3 center = player.transform.position;
            Vector3 eyes = PlayerEyePosition;
            Vector3 look = PlayerLookDirection;
            Vector3 flankRef = flankReference - center;
            flankRef.y = 0f;

            if (hasTarget)
                TryAddCandidate(targetPosition, center, eyes, look, flankRef, true);

            int processed = 0;
            foreach (float radius in ringRadii)
            {
                for (int i = 0; i < anglesPerRing; i++)
                {
                    float angle = 360f * i / anglesPerRing;
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    TryAddCandidate(center + dir * radius, center, eyes, look, flankRef, false);

                    processed++;
                    if (processed % candidatesPerFrame == 0)
                        yield return null; // spread the work over several frames
                }
            }

            FinishEvaluation();
        }

        private void TryAddCandidate(Vector3 rawPosition, Vector3 playerPos, Vector3 eyes,
                                     Vector3 look, Vector3 flankRef, bool isCurrent)
        {
            if (!NavMesh.SamplePosition(rawPosition, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
                return;

            Vector3 pos = hit.position;
            Vector3 fromPlayer = pos - playerPos;
            fromPlayer.y = 0f;
            if (fromPlayer.magnitude < minimumPlayerDistance) return;

            if (!NavMesh.CalculatePath(transform.position, pos, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                return; // unreachable candidates are discarded

            int cornerCount = path.GetCornersNonAlloc(cornerBuffer);
            if (cornerCount < 1) return;

            FlankCandidate c = new FlankCandidate
            {
                Position = pos,
                RouteLength = PathLength(cornerBuffer, cornerCount),
                HasLineOfSight = HasClearSight(pos, eyes),
                ExposureFraction = ComputeExposure(cornerBuffer, cornerCount, eyes, look),
                FlankAngle = flankRef.sqrMagnitude > 0.01f ? Vector3.Angle(flankRef, fromPlayer) : 90f,
                IsCurrentTarget = isCurrent
            };

            working.Add(FlankScoring.Score(c, activeWeights));
        }

        private static float PathLength(Vector3[] corners, int count)
        {
            float length = 0f;
            for (int i = 1; i < count; i++)
                length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        private bool HasClearSight(Vector3 position, Vector3 eyes)
        {
            Vector3 chest = position + Vector3.up;
            return !Physics.Linecast(eyes, chest, obstructionMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Fraction of sampled route points that the player could see right now.</summary>
        private float ComputeExposure(Vector3[] corners, int count, Vector3 eyes, Vector3 look)
        {
            int samples = 0;
            int seen = 0;
            float carry = 0f;

            for (int i = 1; i < count; i++)
            {
                Vector3 a = corners[i - 1];
                Vector3 b = corners[i];
                float segment = Vector3.Distance(a, b);
                float d = carry;

                while (d <= segment)
                {
                    Vector3 p = segment > 0f ? Vector3.Lerp(a, b, d / segment) : a;
                    samples++;
                    if (IsExposedTo(p, eyes, look)) seen++;
                    d += exposureSampleSpacing;
                }

                carry = d - segment;
            }

            return samples == 0 ? 0f : (float)seen / samples;
        }

        private bool IsExposedTo(Vector3 point, Vector3 eyes, Vector3 look)
        {
            Vector3 chest = point + Vector3.up;
            Vector3 toPoint = chest - eyes;

            // Cheap checks first, the raycast last.
            if (toPoint.sqrMagnitude > exposureRange * exposureRange) return false;
            if (Vector3.Angle(look, toPoint) > playerViewHalfAngle) return false;

            return !Physics.Linecast(eyes, chest, obstructionMask, QueryTriggerInteraction.Ignore);
        }

        private void FinishEvaluation()
        {
            isEvaluating = false;
            nextEvaluationTime = Time.time + reevaluateInterval;

            if (State != FlankerState.FLANK) return; // the situation changed while we were scoring

            if (working.Count == 0)
            {
                hasBest = false;
                hasTarget = false;
                if (agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                }
                if (logDecisions) Debug.LogWarning("Flanker found no reachable flank position; will retry.", this);
                return;
            }

            int bestIndex = 0;
            int currentIndex = -1;
            for (int i = 0; i < working.Count; i++)
            {
                if (working[i].Score > working[bestIndex].Score) bestIndex = i;
                if (working[i].IsCurrentTarget) currentIndex = i;
            }

            // Hysteresis: keep the current target unless something is clearly better.
            if (currentIndex >= 0 && bestIndex != currentIndex &&
                working[bestIndex].Score < working[currentIndex].Score + switchMargin)
            {
                bestIndex = currentIndex;
            }

            best = working[bestIndex];
            hasBest = true;

            lastCandidates.Clear();
            lastCandidates.AddRange(working);

            bool changed = !hasTarget || (best.Position - targetPosition).sqrMagnitude > 0.25f;
            if (!changed) return;

            targetPosition = best.Position;
            hasTarget = true;
            agent.isStopped = false;
            agent.SetDestination(targetPosition);

            if (logDecisions)
            {
                Debug.Log(
                    $"Flanker chose ({best.Position.x:F1}, {best.Position.z:F1}) score {best.Score:F2} | " +
                    $"route {best.RouteLength:F1} m, line of sight {best.HasLineOfSight}, " +
                    $"exposure {best.ExposureFraction:F2}, flank {best.FlankAngle:F0} deg | " +
                    $"{working.Count} candidates", this);
            }
        }

        // ------------------------------------------------------------ debug aids

        private void OnDrawGizmos()
        {
            if (!drawGizmos || !Application.isPlaying) return;

            float lo = 1f;
            float hi = 0f;
            foreach (FlankCandidate c in lastCandidates)
            {
                lo = Mathf.Min(lo, c.Score);
                hi = Mathf.Max(hi, c.Score);
            }

            foreach (FlankCandidate c in lastCandidates)
            {
                float t = Mathf.InverseLerp(lo, hi, c.Score);
                Gizmos.color = Color.Lerp(Color.red, Color.green, t);
                Gizmos.DrawSphere(c.Position + Vector3.up * 0.3f, 0.2f);
            }

            if (hasBest)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(best.Position + Vector3.up * 0.3f, 0.5f);
                Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, best.Position + Vector3.up * 0.3f);
            }
        }

        private void OnGUI()
        {
            if (!showOverlay || !Application.isPlaying) return;

            string text = $"Flanker: {State}";
            if (State == FlankerState.FLANK || State == FlankerState.ENGAGE)
                text += $"\nAlert left: {Mathf.Max(0f, alertDuration - (Time.time - lastStimulusTime)):F1} s";
            if (hasBest)
            {
                text += $"\nTarget score {best.Score:F2}" +
                        $"\n route {best.RouteTerm:F2}   visibility {best.VisibilityTerm:F2}" +
                        $"\n exposure {best.ExposureTerm:F2}   flank {best.FlankTerm:F2}";
            }

            GUI.Label(new Rect(Screen.width - 340, 20, 330, 130), text);
        }
    }
}