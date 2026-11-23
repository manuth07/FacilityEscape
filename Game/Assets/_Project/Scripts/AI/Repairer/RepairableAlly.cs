using FacilityEscape.Combat;
using UnityEngine;

namespace FacilityEscape.AI.Repairer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class RepairableAlly : MonoBehaviour
    {
        public Health Health { get; private set; }

        private void Awake()
        {
            Health = GetComponent<Health>();
        }
    }
}
