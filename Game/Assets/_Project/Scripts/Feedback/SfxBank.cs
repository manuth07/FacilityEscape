using UnityEngine;

namespace FacilityEscape.Feedback
{
    /// <summary>
    /// Holds the sound clips (for example from Pixabay) in clearly named slots.
    /// Any empty slot falls back to the generated sound in Sfx, so nothing is ever silent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SfxBank : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

        [Header("Combat")]
        [SerializeField] private AudioClip pulseShot;
        [SerializeField] private AudioClip enemyShot;
        [SerializeField] private AudioClip hitTick;
        [SerializeField] private AudioClip playerHurt;
        [SerializeField] private AudioClip death;
        [SerializeField] private AudioClip alert;

        [Header("Doors")]
        [SerializeField] private AudioClip doorOpen;
        [SerializeField] private AudioClip doorClose;
        [SerializeField] private AudioClip doorDenied;

        [Header("Noise device")]
        [SerializeField] private AudioClip deviceLand;
        [SerializeField] private AudioClip deviceBeep;

        [Header("Pickups and crates")]
        [SerializeField] private AudioClip powerCellPickup;
        [SerializeField] private AudioClip crateScrape;
        [SerializeField] private AudioClip crateImpact;

        public float MasterVolume => masterVolume;

        private void Awake() => Sfx.SetBank(this);

        private void OnDestroy() => Sfx.ClearBank(this);

        public bool TryGetClip(SfxId id, out AudioClip clip)
        {
            switch (id)
            {
                case SfxId.PulseShot: clip = pulseShot; break;
                case SfxId.EnemyShot: clip = enemyShot; break;
                case SfxId.HitTick: clip = hitTick; break;
                case SfxId.PlayerHurt: clip = playerHurt; break;
                case SfxId.Death: clip = death; break;
                case SfxId.Alert: clip = alert; break;
                case SfxId.DoorOpen: clip = doorOpen; break;
                case SfxId.DoorClose: clip = doorClose; break;
                case SfxId.DoorDenied: clip = doorDenied; break;
                case SfxId.DeviceLand: clip = deviceLand; break;
                case SfxId.DeviceBeep: clip = deviceBeep; break;
                case SfxId.PowerCellPickup: clip = powerCellPickup; break;
                case SfxId.CrateScrape: clip = crateScrape; break;
                case SfxId.CrateImpact: clip = crateImpact; break;
                default: clip = null; break;
            }

            return clip != null;
        }
    }
}