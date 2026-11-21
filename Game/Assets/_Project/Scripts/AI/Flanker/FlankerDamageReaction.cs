using UnityEngine;
using FacilityEscape.Combat;

namespace FacilityEscape.AI.Flanker
{
    /// <summary>
    /// Watches the Health component and tells the Flanker when it has been hurt.
    /// Kept separate so the AI logic does not depend on how combat is implemented.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FlankerAI), typeof(Health))]
    public sealed class FlankerDamageReaction : MonoBehaviour
    {
        private FlankerAI flanker;
        private Health health;
        private int lastHealth;

        private void Awake()
        {
            flanker = GetComponent<FlankerAI>();
            health = GetComponent<Health>();
        }

        private void Start()
        {
            lastHealth = health.CurrentHealth;
        }

        private void Update()
        {
            int current = health.CurrentHealth;
            if (current < lastHealth && current > 0)
                flanker.NotifyDamaged();
            lastHealth = current;
        }
    }
}