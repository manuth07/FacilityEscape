using System.Collections;
using UnityEngine;
using FacilityEscape.AI.Common;
using FacilityEscape.Feedback;

[RequireComponent(typeof(Rigidbody))]
public class NoiseDevice : MonoBehaviour
{
    [SerializeField] private float noiseRadius = 15f;
    [SerializeField] private int noisePriority = 1;
    [SerializeField] private float noiseIntensity = 1f;
    [SerializeField] private int beepCount = 3;
    [SerializeField] private float beepInterval = 1f;
    [SerializeField] private float lifetimeAfterLanding = 6f;

    private bool landed;

    private void OnCollisionEnter(Collision collision)
    {
        if (landed) return;
        landed = true;
        Sfx.Play(SfxId.DeviceLand, transform.position, 0.8f);
        StartCoroutine(BeepRoutine());
    }

    private IEnumerator BeepRoutine()
    {
        for (int i = 0; i < beepCount; i++)
        {
            SoundEvents.Emit(transform.position, noiseRadius, noisePriority, noiseIntensity);
            Sfx.Play(SfxId.DeviceBeep, transform.position);
            yield return new WaitForSeconds(beepInterval);
        }
        Destroy(gameObject, Mathf.Max(0f, lifetimeAfterLanding - beepCount * beepInterval));
    }
}