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
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.gKey.wasPressedThisFrame && devices > 0 && Time.time >= nextThrowTime)
            Throw();
    }

    private void Throw()
    {
        devices--;
        nextThrowTime = Time.time + cooldown;

        GameObject device = Instantiate(noiseDevicePrefab,
                                        transform.position + transform.forward * 0.8f,
                                        Quaternion.identity);
        Rigidbody rb = device.GetComponent<Rigidbody>();
        rb.AddForce(transform.forward * throwForce + Vector3.up * 3f, ForceMode.Impulse);
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(20, Screen.height - 40, 250, 30), "Noise devices (G): " + devices);
    }
}