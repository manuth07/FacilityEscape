using UnityEngine;

namespace FacilityEscape.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class PowerCell : MonoBehaviour
    {
        [SerializeField] private PowerCellManager manager;
        private bool collected;
        public bool IsCollected => collected;

        private void OnTriggerEnter(Collider other)
        {
            TryCollect(other.GetComponentInParent<CharacterController>());
        }

        public bool TryCollect(CharacterController collector)
        {
            if (collected || manager == null || !gameObject.activeInHierarchy ||
                !manager.TryRegisterCollection(this, collector))
                return false;

            collected = true;
            gameObject.SetActive(false);
            return true;
        }
    }
}
