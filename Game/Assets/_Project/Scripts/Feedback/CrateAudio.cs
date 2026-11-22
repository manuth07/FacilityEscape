using UnityEngine;

namespace FacilityEscape.Feedback
{
    /// <summary>
    /// Crate sounds: a looping scrape whose volume follows the crate's sliding speed,
    /// and a thud when it hits something hard enough.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CrateAudio : MonoBehaviour
    {
        [Header("Scrape (while sliding)")]
        [SerializeField, Min(0f)] private float scrapeStartSpeed = 0.15f;
        [SerializeField, Min(0.1f)] private float fullVolumeSpeed = 1.5f;
        [SerializeField, Range(0f, 1f)] private float scrapeVolume = 0.7f;
        [SerializeField, Min(0.1f)] private float fadeSpeed = 5f;

        [Header("Impact")]
        [SerializeField, Min(0f)] private float impactMinSpeed = 0.8f;
        [SerializeField, Min(0f)] private float impactCooldown = 0.15f;

        [Header("3D sound")]
        [SerializeField, Min(0.1f)] private float minDistance = 2f;
        [SerializeField, Min(1f)] private float maxDistance = 18f;

        private Rigidbody body;
        private AudioSource source;
        private float nextImpactTime;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            // Created in Start so the SfxBank (which sets up in Awake) is ready.
            source = gameObject.AddComponent<AudioSource>();
            source.clip = Sfx.GetClip(SfxId.CrateScrape);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.volume = 0f;
        }

        private void Update()
        {
            if (source == null || source.clip == null) return;

            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f; // ignore falling, only horizontal sliding makes a scrape
            float speed = velocity.magnitude;

            float target = speed > scrapeStartSpeed
                ? Mathf.Clamp01(speed / fullVolumeSpeed) * scrapeVolume
                : 0f;

            source.volume = Mathf.MoveTowards(source.volume, target, fadeSpeed * Time.deltaTime);

            if (source.volume > 0.01f)
            {
                if (!source.isPlaying) source.Play();
            }
            else if (source.isPlaying)
            {
                source.Stop();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < nextImpactTime) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < impactMinSpeed) return;

            nextImpactTime = Time.time + impactCooldown;
            Sfx.Play(SfxId.CrateImpact, transform.position, Mathf.Clamp01(speed / 6f));
        }
    }
}