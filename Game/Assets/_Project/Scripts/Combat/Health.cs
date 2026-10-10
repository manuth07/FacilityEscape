using UnityEngine;

namespace FacilityEscape.Combat
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 100;
        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsDestroyed => CurrentHealth <= 0;

        private void Awake()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            CurrentHealth = maxHealth;
        }

        public bool TakeDamage(int amount)
        {
            if (!isActiveAndEnabled || amount <= 0 || IsDestroyed)
                return false;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            Debug.Log($"{name} remaining health: {CurrentHealth}/{maxHealth}", this);
            if (IsDestroyed)
            {
                Debug.Log($"{name} destroyed", this);
                gameObject.SetActive(false);
            }
            return true;
        }
    }
}
