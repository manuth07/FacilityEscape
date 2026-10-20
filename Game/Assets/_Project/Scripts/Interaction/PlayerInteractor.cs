using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private LayerMask interactMask = ~0;

    private IInteractable current;

    private void Update()
    {
        current = null;
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask,
                            QueryTriggerInteraction.Ignore))
        {
            current = hit.collider.GetComponentInParent<IInteractable>();
        }

        if (current != null && Input.GetKeyDown(interactKey))
            current.Interact();
    }

    private void OnGUI()
    {
        if (current == null) return;
        GUI.Label(new Rect(Screen.width / 2f - 80, Screen.height / 2f + 30, 220, 30),
                  "[E] " + current.Prompt);
    }
}