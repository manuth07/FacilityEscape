using System;
using UnityEngine;

namespace FacilityEscape.AI.Common
{
    public readonly struct SoundEvent
    {
        public Vector3 Position { get; }
        public float HearingRadius { get; }
        public int Priority { get; }
        public float Intensity { get; }
        public float EmittedAt { get; }

        public SoundEvent(Vector3 position, float hearingRadius, int priority, float intensity,
            float? emittedAt = null)
        {
            Position = position;
            HearingRadius = hearingRadius;
            Priority = priority;
            Intensity = intensity;
            EmittedAt = emittedAt ?? Time.time;
        }
    }

    // Logical AI signals only: this system does not play audio.
    public static class SoundEvents
    {
        public static event Action<SoundEvent> Emitted;

        public static void Emit(Vector3 position, float hearingRadius,
            int priority = 0, float intensity = 1f)
        {
            if (hearingRadius <= 0f) return;
            Emitted?.Invoke(new SoundEvent(position, hearingRadius, priority,
                Mathf.Max(0f, intensity)));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetListeners()
        {
            Emitted = null;
        }
    }
}
