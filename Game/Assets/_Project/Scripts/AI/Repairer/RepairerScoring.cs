using UnityEngine;

namespace FacilityEscape.AI.Repairer
{
    public enum RepairerAction { Hold, Repair, Escape, Return }

    public static class RepairerScoring
    {
        public static float Repair(float missingFraction, float pathLength)
        {
            return 0.7f * Mathf.Clamp01(missingFraction) + 0.19f / (1f + pathLength);
        }

        public static float Escape(float proximity, float freshness)
        {
            return 0.9f + 0.05f * Mathf.Clamp01(proximity) + 0.05f * Mathf.Clamp01(freshness);
        }

        public static float Return(float pathLength)
        {
            return Mathf.Clamp01(pathLength / 8f);
        }

        public static RepairerAction Select(float repair, float escape, float returnScore)
        {
            float highest = Mathf.Max(repair, escape, returnScore);
            if (highest <= 0f) return RepairerAction.Hold;
            if (escape == highest) return RepairerAction.Escape;
            return repair == highest ? RepairerAction.Repair : RepairerAction.Return;
        }
    }
}
