using UnityEngine;
using UnityEngine.InputSystem;
using FacilityEscape.AI.Common;

namespace FacilityEscape.Combat
{
    [DisallowMultipleComponent]
    public sealed class PulseGun : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private MonoBehaviour playerMovement;
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField, Min(0.1f)] private float range = 50f;
        [SerializeField, Min(0.1f)] private float shotsPerSecond = 4f;
        [SerializeField, Min(0f)] private float flashDuration = 0.06f;

        [SerializeField, Min(1)] private int damagePerShot = 25;

        [SerializeField, Min(0f)] private float gunshotHearingRadius = 15f;
        [SerializeField] private int gunshotPriority = 10;
        [SerializeField, Min(0f)] private float gunshotIntensity = 2f;

        private float nextShotTime;
        private float flashEndTime;
        public int ShotsFired { get; private set; }

        private void OnEnable()
        {
            nextShotTime = Time.time;
            if (muzzleFlash != null) muzzleFlash.SetActive(false);
        }

        private void OnDisable()
        {
            if (muzzleFlash != null) muzzleFlash.SetActive(false);
        }

        private void Update()
        {
            if (muzzleFlash != null && muzzleFlash.activeSelf && Time.time >= flashEndTime)
                muzzleFlash.SetActive(false);

            if (Application.isFocused && Cursor.lockState == CursorLockMode.Locked &&
                Mouse.current != null && Mouse.current.leftButton.isPressed)
                TryFire();
        }

        public bool TryFire()
        {
            if (!isActiveAndEnabled || playerCamera == null || !playerCamera.isActiveAndEnabled ||
                playerMovement == null || !playerMovement.enabled || Time.time < nextShotTime)
                return false;

            nextShotTime = Time.time + 1f / shotsPerSecond;
            ShotsFired++;
            SoundEvents.Emit(playerRoot != null ? playerRoot.position : transform.position,
                gunshotHearingRadius, gunshotPriority, gunshotIntensity);
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool found = false;
            RaycastHit closest = default;
            foreach (RaycastHit hit in hits)
            {
                if (playerRoot != null && hit.collider.transform.IsChildOf(playerRoot))
                    continue;
                if (!found || hit.distance < closest.distance)
                {
                    closest = hit;
                    found = true;
                }
            }

            if (found)
            {
                string path = closest.collider.name;
                for (Transform parent = closest.collider.transform.parent; parent != null; parent = parent.parent)
                    path = parent.name + "/" + path;
                Debug.Log($"Pulse Gun hit: {path} ({closest.distance:F2} m)", this);
                Health health = closest.collider.GetComponentInParent<Health>();
                if (health != null) health.TakeDamage(damagePerShot);
            }
            else
            {
                Debug.Log($"Pulse Gun missed: no solid target within {range:F0} m", this);
            }

            if (muzzleFlash != null)
            {
                muzzleFlash.SetActive(true);
                flashEndTime = Time.time + flashDuration;
            }
            return true;
        }
    }
}
