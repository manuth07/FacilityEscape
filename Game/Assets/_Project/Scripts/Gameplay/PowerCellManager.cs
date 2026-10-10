using System.Collections.Generic;
using UnityEngine;

namespace FacilityEscape.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PowerCellManager : MonoBehaviour
    {
        public const int TotalCells = 3;
        [SerializeField] private CharacterController player;
        [SerializeField] private PowerCell[] cells = new PowerCell[TotalCells];

        private readonly HashSet<PowerCell> collectedCells = new HashSet<PowerCell>();
        public int CollectedCount => collectedCells.Count;

        internal bool TryRegisterCollection(PowerCell cell, CharacterController collector)
        {
            if (cell == null || player == null || collector != player ||
                collectedCells.Count >= TotalCells)
                return false;

            bool registered = false;
            foreach (PowerCell configuredCell in cells)
            {
                if (configuredCell == cell)
                {
                    registered = true;
                    break;
                }
            }

            if (!registered || !collectedCells.Add(cell))
                return false;

            Debug.Log($"Power Cell collected: {CollectedCount}/{TotalCells}", this);
            return true;
        }
    }
}
