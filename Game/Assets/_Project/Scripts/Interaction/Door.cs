using UnityEngine;
using UnityEngine.AI;

public class Door : MonoBehaviour, IInteractable
{
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float speed = 3f;
    [SerializeField] private NavMeshObstacle obstacle; // optional: blocks robots while closed
    [SerializeField] private bool startsLocked;
    [SerializeField] private string lockedPrompt = "Locked - find the power cells";

    private bool isOpen;
    private bool isLocked;
    private Quaternion closedRot;
    private Quaternion openRot;

    public string Prompt => isLocked ? lockedPrompt : (isOpen ? "Close door" : "Open door");
    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;

    private void Start()
    {
        closedRot = transform.localRotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
        isLocked = startsLocked;
        if (obstacle != null) obstacle.enabled = true;
    }

    public void Interact()
    {
        if (isLocked) return;
        isOpen = !isOpen;
        if (obstacle != null) obstacle.enabled = !isOpen;
    }

    public void Unlock() => isLocked = false;

    private void Update()
    {
        Quaternion target = isOpen ? openRot : closedRot;
        if (Quaternion.Angle(transform.localRotation, target) < 0.1f) return;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * speed);
    }
}