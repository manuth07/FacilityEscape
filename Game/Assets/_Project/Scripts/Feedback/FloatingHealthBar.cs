using UnityEngine;
using UnityEngine.UI;
using FacilityEscape.Combat;

namespace FacilityEscape.Feedback
{
    /// <summary>World-space health bar above an agent. Shows for a few seconds after it is damaged.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class FloatingHealthBar : MonoBehaviour
    {
        [SerializeField] private float height = 2.4f;
        [SerializeField, Min(0.2f)] private float width = 1.2f;
        [SerializeField, Min(0.5f)] private float visibleSeconds = 3f;
        [SerializeField] private bool alwaysVisible;
        [SerializeField] private Color fillColor = new Color(1f, 0.35f, 0.3f);

        private Health health;
        private Canvas canvas;
        private RectTransform fillRect;
        private Camera cam;
        private int lastHp;
        private float hideAt = -1f;

        private void Awake()
        {
            health = GetComponent<Health>();
            Build();
        }

        private void Start()
        {
            lastHp = health.CurrentHealth;
            canvas.gameObject.SetActive(alwaysVisible);
        }

        private void LateUpdate()
        {
            int hp = health.CurrentHealth;
            if (hp < lastHp) hideAt = Time.time + visibleSeconds;
            lastHp = hp;

            bool show = alwaysVisible || Time.time < hideAt;
            if (canvas.gameObject.activeSelf != show) canvas.gameObject.SetActive(show);
            if (!show) return;

            float frac = hp / (float)Mathf.Max(1, health.MaxHealth);
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);

            if (cam == null || !cam.isActiveAndEnabled) cam = FindActiveCamera();
            if (cam != null) canvas.transform.rotation = cam.transform.rotation; // face the camera
        }

        private static Camera FindActiveCamera()
        {
            if (Camera.main != null) return Camera.main;
            Camera[] all = Camera.allCameras;
            return all.Length > 0 ? all[0] : null;
        }

        private void Build()
        {
            GameObject go = new GameObject("FloatingHealthBar", typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * height;

            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 12f);
            float scale = width / 100f;
            rt.localScale = new Vector3(scale, scale, scale);

            Image back = NewImage("Back", go.transform, new Color(0f, 0f, 0f, 0.7f));
            Stretch(back.rectTransform);

            Image fill = NewImage("Fill", back.transform, fillColor);
            Stretch(fill.rectTransform);
            fillRect = fill.rectTransform;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}