using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerThrower : MonoBehaviour
{
    [SerializeField] private GameObject noiseDevicePrefab;
    [SerializeField] private float throwForce = 15f;
    [SerializeField] private float cooldown = 1.5f;
    [SerializeField] private int devices = 5;

    private float nextThrowTime;

    public int Devices => devices;

    private void Update()
    {
        if (!FacilityEscape.Gameplay.GameFlowController.GameplayAllowed) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (Cursor.lockState == CursorLockMode.Locked
            && keyboard.gKey.wasPressedThisFrame
            && devices > 0 && Time.time >= nextThrowTime)
        {
            TryThrow();
        }
    }

    public bool TryThrow()
    {
        if (!FacilityEscape.Gameplay.GameFlowController.GameplayAllowed) return false;
        var health = GetComponentInParent<FacilityEscape.Combat.Health>();
        if (!isActiveAndEnabled || (health != null && health.IsDestroyed) || devices <= 0 ||
            Time.time < nextThrowTime || noiseDevicePrefab == null ||
            noiseDevicePrefab.GetComponent<Rigidbody>() == null) return false;
        devices--;
        nextThrowTime = Time.time + cooldown;

        GameObject device = Instantiate(noiseDevicePrefab,
                                        transform.position + transform.forward * 0.8f,
                                        Quaternion.identity);
        Rigidbody rb = device.GetComponent<Rigidbody>();
        rb.AddForce(transform.forward * throwForce + Vector3.up * 3f, ForceMode.Impulse);
        return true;
    }

    
}
