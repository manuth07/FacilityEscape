using UnityEngine;
using FacilityEscape.Gameplay;

namespace FacilityEscape.Feedback
{
    /// <summary>Plays a sound when the number of collected power cells increases.</summary>
    [DisallowMultipleComponent]
    public sealed class PowerCellFeedback : MonoBehaviour
    {
        [SerializeField] private PowerCellManager manager;

        private int lastCount;

        private void Start()
        {
            if (manager == null) manager = FindFirstObjectByType<PowerCellManager>();
            lastCount = manager != null ? manager.CollectedCount : 0;
        }

        private void Update()
        {
            if (manager == null) return;

            int count = manager.CollectedCount;
            if (count > lastCount) Sfx.Play2D(SfxId.PowerCellPickup);
            lastCount = count;
        }
    }
}