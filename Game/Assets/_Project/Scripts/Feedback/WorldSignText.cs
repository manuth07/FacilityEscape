using UnityEngine;

namespace FacilityEscape.Feedback
{
    // Keeps the built-in dynamic font atlas bound to the depth-tested world-text shader.
    [ExecuteAlways, RequireComponent(typeof(TextMesh))]
    public sealed class WorldSignText : MonoBehaviour
    {
        private MaterialPropertyBlock properties;
        private void OnEnable() { Font.textureRebuilt += FontChanged; Refresh(); }
        private void OnDisable() => Font.textureRebuilt -= FontChanged;
        private void FontChanged(Font font) { if (GetComponent<TextMesh>().font == font) Refresh(); }
        public void Refresh()
        {
            var text = GetComponent<TextMesh>();
            if (text.font == null || text.font.material == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            var renderer = GetComponent<MeshRenderer>();
            renderer.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex", text.font.material.mainTexture);
            renderer.SetPropertyBlock(properties);
        }
    }
}
