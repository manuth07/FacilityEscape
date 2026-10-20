using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CratePusher : MonoBehaviour
{
    [SerializeField] private float pushForce = 150f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        body.AddForce(pushDir * pushForce, ForceMode.Force);
    }
}