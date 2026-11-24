using FacilityEscape.Combat;
using UnityEngine;
using System.Collections.Generic;

namespace FacilityEscape.AI.Repairer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class RepairableAlly : MonoBehaviour
    {
        private static readonly HashSet<RepairableAlly> registered = new HashSet<RepairableAlly>();
        public static IEnumerable<RepairableAlly> Registered => registered;
        public Health Health { get; private set; }

        private void Awake()
        {
            Health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            Health = GetComponent<Health>();
            registered.Add(this);
        }

        private void OnDisable() => registered.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => registered.Clear();
    }
}
