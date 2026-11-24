using UnityEngine;
using UnityEngine.AI;
using FacilityEscape.Feedback;

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
    private NavMeshObstacle openPanelObstacle;

    public string Prompt => isLocked ? lockedPrompt : (isOpen ? "Close door" : "Open door");
    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;

    private void Start()
    {
        closedRot = transform.localRotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
        isLocked = startsLocked;
        if (obstacle != null) obstacle.enabled = true;
        if (obstacle != null)
        {
            // The opening blocker is still released only when the door is fully open.
            // Keep the physical open leaf carved too, so agents route around its side.
            var panel = new GameObject("OpenPanelNavigation");
            panel.transform.SetParent(obstacle.transform, false);
            openPanelObstacle = panel.AddComponent<NavMeshObstacle>();
            openPanelObstacle.shape = obstacle.shape;
            openPanelObstacle.size = obstacle.size;
            openPanelObstacle.center = obstacle.center;
            openPanelObstacle.carving = true;
            openPanelObstacle.carveOnlyStationary = true;
            openPanelObstacle.carvingTimeToStationary = 0.1f;
            openPanelObstacle.enabled = false;
        }
    }

    public void Interact()
    {
        if (isLocked)
        {
            Sfx.Play(SfxId.DoorDenied, transform.position);
            return;
        }

        isOpen = !isOpen;
        // Closing blocks navigation immediately. Opening keeps the blocker until
        // the physical panel is nearly at its open rotation.
        UpdateNavigationBlocker();
        Sfx.Play(isOpen ? SfxId.DoorOpen : SfxId.DoorClose, transform.position);
    }

    public void Unlock() => isLocked = false;

    private void Update()
    {
        Quaternion target = isOpen ? openRot : closedRot;
        if (Quaternion.Angle(transform.localRotation, target) >= 0.1f)
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * speed);
        UpdateNavigationBlocker();
    }

    private void UpdateNavigationBlocker()
    {
        if (obstacle == null) return;
        bool shouldBlock = !isOpen || Quaternion.Angle(transform.localRotation, openRot) > 0.5f;
        if (obstacle.enabled != shouldBlock) obstacle.enabled = shouldBlock;
        if (openPanelObstacle != null && openPanelObstacle.enabled == shouldBlock)
            openPanelObstacle.enabled = !shouldBlock;
    }
}
