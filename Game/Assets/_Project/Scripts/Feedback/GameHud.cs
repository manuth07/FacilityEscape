using UnityEngine;
using UnityEngine.UI;
using FacilityEscape.Combat;
using FacilityEscape.Gameplay;

namespace FacilityEscape.Feedback
{
    /// <summary>
    /// Runtime-built HUD. It only reads state (Health, PlayerThrower, PowerCellManager),
    /// so it does not depend on or modify any other system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHud : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerThrower thrower;
        [SerializeField] private PowerCellManager powerCells;

        [Header("Look")]
        [SerializeField] private Color healthyColor = new Color(0.2f, 0.9f, 0.45f);
        [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.35f, 0.3f);
        [SerializeField, Min(0.5f)] private float targetVisibleSeconds = 2f;

        private Font font;
        private Image playerFill, playerTrail, vignette, targetFill, targetTrail;
        private Text playerText, targetNameText, targetValueText, infoText, centerText;
        private GameObject targetPanel;
        private readonly Image[] crosshair = new Image[4];
        private readonly Image[] hitMarker = new Image[4];

        private Health aimTarget, lastTarget, shownTarget;
        private string shownName = "";
        private float targetVisibleUntil;
        private float playerTrailFrac = 1f, targetTrailFrac = 1f;
        private int lastPlayerHp = -1;
        private int lastDevices = -2, lastCells = -2;
        private float flash;
        private float hitTimer;
        private bool hitKill;

        private const float HitMarkerSeconds = 0.18f;

        private void Awake()
        {
            Build();
        }

        private void Start()
        {
            if (playerHealth == null)
            {
                var controller = FindFirstObjectByType<FacilityEscape.Player.FirstPersonController>();
                if (controller != null) playerHealth = controller.GetComponent<Health>();
            }
            if (thrower == null) thrower = FindFirstObjectByType<PlayerThrower>();
            if (powerCells == null) powerCells = FindFirstObjectByType<PowerCellManager>();

            if (playerHealth == null)
                Debug.LogWarning("GameHud: no player Health found. Add Health to the Player or assign it.", this);
            if (FindFirstObjectByType<AudioListener>() == null)
                Debug.LogWarning("GameHud: there is no AudioListener in the scene, so sounds will be silent.", this);
        }

        // ------------------------------------------------------------ public API

        /// <summary>Called every frame by PulseGunFeedback with whatever the crosshair is over.</summary>
        public void SetAim(Health target)
        {
            aimTarget = target;
        }

        public void ShowHitMarker(bool kill)
        {
            hitTimer = HitMarkerSeconds;
            hitKill = kill;
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdatePlayer(dt);
            UpdateTarget(dt);
            UpdateInfo();
            UpdateMarkers(dt);
        }

        private void UpdatePlayer(float dt)
        {
            if (playerHealth == null)
            {
                playerText.text = "--";
                SetFraction(playerFill, 0f);
                SetFraction(playerTrail, 0f);
                return;
            }

            int hp = playerHealth.CurrentHealth;
            float frac = hp / (float)Mathf.Max(1, playerHealth.MaxHealth);

            if (lastPlayerHp < 0) lastPlayerHp = hp;
            if (hp < lastPlayerHp)
            {
                flash = 1f;
                Sfx.Play2D(SfxId.PlayerHurt);
            }
            lastPlayerHp = hp;

            playerTrailFrac = frac > playerTrailFrac
                ? frac
                : Mathf.MoveTowards(playerTrailFrac, frac, 0.35f * dt);

            SetFraction(playerFill, frac);
            SetFraction(playerTrail, playerTrailFrac);
            playerFill.color = HealthColor(frac);
            playerText.text = hp + " / " + playerHealth.MaxHealth;

            flash = Mathf.MoveTowards(flash, 0f, 2.5f * dt);
            float low = (frac < 0.3f && hp > 0)
                ? 0.08f + 0.06f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f))
                : 0f;
            vignette.color = new Color(1f, 0f, 0f, Mathf.Max(flash * 0.3f, low));

            centerText.gameObject.SetActive(hp <= 0);
        }

        private void UpdateTarget(float dt)
        {
            if (aimTarget != null && !aimTarget.IsDestroyed)
            {
                lastTarget = aimTarget;
                targetVisibleUntil = Time.unscaledTime + targetVisibleSeconds;
            }

            bool show = lastTarget != null && Time.unscaledTime < targetVisibleUntil;
            if (targetPanel.activeSelf != show) targetPanel.SetActive(show);
            if (!show) return;

            float frac = lastTarget.CurrentHealth / (float)Mathf.Max(1, lastTarget.MaxHealth);

            if (lastTarget != shownTarget)
            {
                shownTarget = lastTarget;
                targetTrailFrac = frac;
                shownName = lastTarget.gameObject.name.Replace("(Clone)", "").Trim().ToUpperInvariant();
                targetNameText.text = shownName;
            }

            targetTrailFrac = frac > targetTrailFrac
                ? frac
                : Mathf.MoveTowards(targetTrailFrac, frac, 0.35f * dt);

            SetFraction(targetFill, frac);
            SetFraction(targetTrail, targetTrailFrac);
            targetValueText.text = lastTarget.CurrentHealth + " / " + lastTarget.MaxHealth;
        }

        private void UpdateInfo()
        {
            int devices = thrower != null ? thrower.Devices : -1;
            int cells = powerCells != null ? powerCells.CollectedCount : -1;
            if (devices == lastDevices && cells == lastCells) return;

            lastDevices = devices;
            lastCells = cells;

            string deviceText = devices >= 0 ? devices.ToString() : "-";
            string cellText = cells >= 0 ? cells + " / " + PowerCellManager.TotalCells : "-";
            infoText.text = "NOISE DEVICES   " + deviceText + "\nPOWER CELLS   " + cellText;
        }

        private void UpdateMarkers(float dt)
        {
            bool onTarget = aimTarget != null && !aimTarget.IsDestroyed;
            Color crosshairColor = onTarget ? enemyColor : Color.white;
            for (int i = 0; i < crosshair.Length; i++) crosshair[i].color = crosshairColor;

            hitTimer = Mathf.Max(0f, hitTimer - dt);
            Color marker = hitKill ? dangerColor : Color.white;
            marker.a = hitTimer / HitMarkerSeconds;
            for (int i = 0; i < hitMarker.Length; i++) hitMarker[i].color = marker;
        }

        private Color HealthColor(float f)
        {
            return f > 0.5f
                ? Color.Lerp(warningColor, healthyColor, (f - 0.5f) * 2f)
                : Color.Lerp(dangerColor, warningColor, f * 2f);
        }

        // ----------------------------------------------------------------- build

        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            GameObject root = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform parent = root.transform;

            vignette = NewImage("DamageTint", parent, new Color(1f, 0f, 0f, 0f));
            Stretch(vignette.rectTransform);

            BuildPlayerPanel(parent);
            BuildTargetPanel(parent);
            BuildInfoPanel(parent);
            BuildCrosshair(parent);

            centerText = NewText("CenterText", parent, 64, TextAnchor.MiddleCenter, dangerColor);
            centerText.text = "SYSTEM FAILURE";
            Place(centerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                  new Vector2(0f, 140f), new Vector2(900f, 100f));
            centerText.gameObject.SetActive(false);
        }

        private void BuildPlayerPanel(Transform parent)
        {
            Image panel = NewImage("PlayerPanel", parent, new Color(0f, 0f, 0f, 0.55f));
            Place(panel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(30f, 30f), new Vector2(380f, 96f));

            Text title = NewText("Title", panel.transform, 22, TextAnchor.MiddleLeft, Color.white);
            title.text = "HEALTH";
            Place(title.rectTransform, Vector2.zero, Vector2.zero, new Vector2(16f, 62f), new Vector2(180f, 28f));

            playerText = NewText("Value", panel.transform, 24, TextAnchor.MiddleRight, Color.white);
            Place(playerText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(184f, 62f), new Vector2(180f, 28f));

            BuildBar(panel.transform, new Vector2(16f, 16f), new Vector2(348f, 38f), out playerTrail, out playerFill);
        }

        private void BuildTargetPanel(Transform parent)
        {
            Image panel = NewImage("TargetPanel", parent, new Color(0f, 0f, 0f, 0.55f));
            Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                  new Vector2(0f, -30f), new Vector2(460f, 88f));
            targetPanel = panel.gameObject;

            targetNameText = NewText("Name", panel.transform, 22, TextAnchor.MiddleLeft, Color.white);
            Place(targetNameText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(16f, 54f), new Vector2(290f, 28f));

            targetValueText = NewText("Value", panel.transform, 22, TextAnchor.MiddleRight, Color.white);
            Place(targetValueText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(300f, 54f), new Vector2(144f, 28f));

            BuildBar(panel.transform, new Vector2(16f, 14f), new Vector2(428f, 26f), out targetTrail, out targetFill);
            targetFill.color = enemyColor;
            targetPanel.SetActive(false);
        }

        private void BuildInfoPanel(Transform parent)
        {
            Image panel = NewImage("InfoPanel", parent, new Color(0f, 0f, 0f, 0.55f));
            Place(panel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                  new Vector2(-30f, 30f), new Vector2(330f, 84f));

            infoText = NewText("Info", panel.transform, 22, TextAnchor.MiddleLeft, Color.white);
            Place(infoText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(16f, 8f), new Vector2(300f, 68f));
        }

        private void BuildCrosshair(Transform parent)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

            for (int i = 0; i < 4; i++)
            {
                Image bar = NewImage("Crosshair" + i, parent, Color.white);
                bool vertical = i < 2;
                Place(bar.rectTransform, center, center, dirs[i] * 14f,
                      vertical ? new Vector2(3f, 12f) : new Vector2(12f, 3f));
                crosshair[i] = bar;
            }

            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + 90f * i;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                Image bar = NewImage("HitMarker" + i, parent, new Color(1f, 1f, 1f, 0f));
                Place(bar.rectTransform, center, center, dir * 26f, new Vector2(4f, 14f));
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                hitMarker[i] = bar;
            }
        }

        private void BuildBar(Transform parent, Vector2 pos, Vector2 size, out Image trail, out Image fill)
        {
            Image back = NewImage("BarBack", parent, new Color(1f, 1f, 1f, 0.12f));
            Place(back.rectTransform, Vector2.zero, Vector2.zero, pos, size);

            trail = NewImage("Trail", back.transform, new Color(1f, 1f, 1f, 0.55f));
            fill = NewImage("Fill", back.transform, healthyColor);
            SetFraction(trail, 1f);
            SetFraction(fill, 1f);
        }

        // --------------------------------------------------------------- helpers

        private Image NewImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text NewText(string name, Transform parent, int size, TextAnchor align, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = align;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // A bar fill is a rectangle whose right anchor moves, so no sprite is needed.
        private static void SetFraction(Image image, float fraction)
        {
            RectTransform rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}