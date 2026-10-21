using UnityEngine;
using UnityEngine.AI;

public class Door : MonoBehaviour, IInteractable
{
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float speed = 3f;
    [SerializeField] private NavMeshObstacle obstacle; // optional: blocks robots while closed

    private bool isOpen;
    private Quaternion closedRot;
    private Quaternion openRot;

    public string Prompt => isOpen ? "Close door" : "Open door";
    public bool IsOpen => isOpen;

    private void Start()
    {
        closedRot = transform.localRotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
        if (obstacle != null) obstacle.enabled = true;
    }

    public void Interact()
    {
        isOpen = !isOpen;
        if (obstacle != null) obstacle.enabled = !isOpen;
    }

    private void Update()
    {
        Quaternion target = isOpen ? openRot : closedRot;
        if (Quaternion.Angle(transform.localRotation, target) < 0.1f) return;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * speed);
    }
}