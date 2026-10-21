using UnityEngine;
using FacilityEscape.Gameplay;

public class ExitDoorUnlocker : MonoBehaviour
{
    [SerializeField] private PowerCellManager powerCellManager;
    [SerializeField] private Door door;
    [SerializeField] private int requiredCells = PowerCellManager.TotalCells;

    private bool unlocked;

    private void Update()
    {
        if (unlocked || powerCellManager == null || door == null) return;

        if (powerCellManager.CollectedCount >= requiredCells)
        {
            door.Unlock();
            unlocked = true;
        }
    }
}