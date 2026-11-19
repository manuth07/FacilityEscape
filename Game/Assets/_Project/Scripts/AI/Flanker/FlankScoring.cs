using UnityEngine;

namespace FacilityEscape.AI.Flanker
{
    /// <summary>
    /// Pure utility maths: turns raw measurements into normalised terms and a weighted score.
    /// No Unity scene access, so it is easy to reason about and defend.
    /// </summary>
    public static class FlankScoring
    {
        public static FlankCandidate Score(FlankCandidate c, FlankWeights w)
        {
            // Shorter route is better: linear falloff, clamped at maxRouteLength.
            c.RouteTerm = 1f - Mathf.Clamp01(c.RouteLength / w.maxRouteLength);

            // Binary: the position either has a clear line to the player or it does not.
            c.VisibilityTerm = c.HasLineOfSight ? 1f : 0f;

            // Less of the route inside the player's view cone is better.
            c.ExposureTerm = 1f - Mathf.Clamp01(c.ExposureFraction);

            // 0 degrees = same side as the Flanker, 180 degrees = opposite side.
            c.FlankTerm = Mathf.Clamp01(c.FlankAngle / 180f);

            float total = w.Total;
            c.Score = total <= 0f
                ? 0f
                : (w.route * c.RouteTerm
                   + w.visibility * c.VisibilityTerm
                   + w.exposure * c.ExposureTerm
                   + w.flank * c.FlankTerm) / total;

            return c;
        }
    }
}