using System;
using UnityEngine;

namespace FacilityEscape.AI.Flanker
{
    /// <summary>
    /// Tunable weights for the Flanker's utility function.
    /// Weights are normalised by their sum, so the final score always lies in [0, 1].
    /// </summary>
    [Serializable]
    public sealed class FlankWeights
    {
        [Min(0f)] public float route = 0.30f;
        [Min(0f)] public float visibility = 0.30f;
        [Min(0f)] public float exposure = 0.25f;
        [Min(0f)] public float flank = 0.15f;

        [Tooltip("A route this long or longer scores 0 for the route term.")]
        [Min(1f)] public float maxRouteLength = 45f;

        public float Total => route + visibility + exposure + flank;
    }
}