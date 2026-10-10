using UnityEngine;
using UnityEngine.InputSystem;

namespace FacilityEscape.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ExitTerminal : MonoBehaviour
    {
        [SerializeField] private PowerCellManager powerCellManager;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private MonoBehaviour playerMovement;
        [SerializeField] private Camera originalSceneCamera;
        [SerializeField] private Renderer statusRenderer;
        [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;

        private MaterialPropertyBlock statusProperties;
        private bool visualInitialized;
        private bool lastUnlocked;
        private GUIStyle winStyle;

        public bool IsUnlocked => powerCellManager != null &&
            powerCellManager.CollectedCount >= PowerCellManager.TotalCells;
        public bool HasEscaped { get; private set; }

        private void Update()
        {
            RefreshVisual();
            if (!HasEscaped && Application.isFocused &&
                Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                TryActivate();
        }

        public bool CanInteract()
        {
            if (playerCamera == null || !playerCamera.isActiveAndEnabled)
                return false;

            RaycastHit hit;
            return Physics.Raycast(playerCamera.transform.position,
                playerCamera.transform.forward, out hit, interactionDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<ExitTerminal>() == this;
        }

        public bool TryActivate()
        {
            if (HasEscaped || !CanInteract())
                return false;

            RefreshVisual();
            if (!IsUnlocked)
            {
                int collected = powerCellManager != null ? powerCellManager.CollectedCount : 0;
                Debug.Log($"Exit terminal locked: more power cells required ({collected}/{PowerCellManager.TotalCells}).", this);
                return false;
            }

            HasEscaped = true;
            // The current movement component restores the original camera when disabled.
            // Keep the win view on the player's camera after stopping movement.
            if (playerMovement != null) playerMovement.enabled = false;
            if (originalSceneCamera != null) originalSceneCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("FACILITY ESCAPE SUCCESS", this);
            return true;
        }

        private void RefreshVisual()
        {
            if (statusRenderer == null) return;
            bool unlocked = IsUnlocked;
            if (visualInitialized && lastUnlocked == unlocked) return;

            if (statusProperties == null) statusProperties = new MaterialPropertyBlock();
            statusRenderer.GetPropertyBlock(statusProperties);
            Color color = unlocked ? new Color(0.05f, 1f, 0.7f) : new Color(1f, 0.18f, 0.025f);
            statusProperties.SetColor("_BaseColor", color);
            statusProperties.SetColor("_EmissionColor", color * 2.5f);
            statusRenderer.SetPropertyBlock(statusProperties);
            lastUnlocked = unlocked;
            visualInitialized = true;
        }

        private void OnGUI()
        {
            if (!HasEscaped) return;
            if (winStyle == null) winStyle = new GUIStyle(GUI.skin.label);
            // Reapply style during each GUI pass, including resized Game views.
            winStyle.alignment = TextAnchor.MiddleCenter;
            winStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.05f), 24, 64);
            winStyle.fontStyle = FontStyle.Bold;
            winStyle.wordWrap = true;
            winStyle.normal.textColor = Color.white;

            float width = Mathf.Min(800f, Screen.width - 32f);
            Rect panel = new Rect((Screen.width - width) * 0.5f,
                (Screen.height - 130f) * 0.5f, width, 130f);
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(panel, "ESCAPE SUCCESSFUL", winStyle);
            GUI.color = previousColor;
        }
    }
}
