using UnityEngine;
using FacilityEscape.Combat;
using FacilityEscape.Player;
using FacilityEscape.Feedback;

namespace FacilityEscape.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HealthPickup : MonoBehaviour
    {
        [SerializeField, Min(1)] private int healAmount = 30;
        private bool collected;

        private void OnTriggerEnter(Collider other) => TryCollect(other.GetComponentInParent<CharacterController>());
        private void OnTriggerStay(Collider other) => TryCollect(other.GetComponentInParent<CharacterController>());

        public bool TryCollect(CharacterController player)
        {
            if (collected || !isActiveAndEnabled || player == null || !player.enabled ||
                !player.gameObject.activeInHierarchy || player.GetComponent<FirstPersonController>() == null)
                return false;
            Health health = player.GetComponentInParent<Health>();
            if (health == null || !health.isActiveAndEnabled || health.IsDestroyed) return false;
            int before = health.CurrentHealth;
            if (!health.Heal(healAmount)) return false;
            collected = true;
            Sfx.Play(SfxId.PowerCellPickup, transform.position);
            Debug.Log($"Health pickup: +{health.CurrentHealth - before} HP ({health.CurrentHealth}/{health.MaxHealth})", this);
            gameObject.SetActive(false);
            return true;
        }
    }
}
