using UnityEngine;
using UnityEngine.InputSystem;

namespace FacilityEscape.AI.Common
{
    // Temporary manual test input; remove this scene object after hearing validation.
    public sealed class SoundTestInput : MonoBehaviour
    {
        [SerializeField] private Transform soundSource;
        [SerializeField, Min(0f)] private float hearingRadius = 10f;

        private void Update()
        {
            if (Application.isPlaying && Keyboard.current != null &&
                Keyboard.current.tKey.wasPressedThisFrame)
                EmitTestSound();
        }

        public void EmitTestSound()
        {
            if (soundSource != null)
                SoundEvents.Emit(soundSource.position, hearingRadius);
        }
    }
}
