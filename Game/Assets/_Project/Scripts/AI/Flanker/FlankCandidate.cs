using UnityEngine;

namespace FacilityEscape.AI.Flanker
{
    /// <summary>One scored candidate position around the player.</summary>
    public struct FlankCandidate
    {
        // Raw measurements
        public Vector3 Position;
        public float RouteLength;
        public bool HasLineOfSight;
        public float ExposureFraction;
        public float FlankAngle;
        public bool IsCurrentTarget;

        // Normalised terms (0..1, higher is better)
        public float RouteTerm;
        public float VisibilityTerm;
        public float ExposureTerm;
        public float FlankTerm;

        public float Score;
    }
}