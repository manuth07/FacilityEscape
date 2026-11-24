using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Events;

namespace FacilityEscape.Gameplay
{
    // One overlay owner for every non-gameplay screen, with shared typography and button treatment.
    public sealed class MissionFlowView : MonoBehaviour
    {
        private GameFlowController flow;
        private Font font;
        private RectTransform root;
        private GameObject screen;
        private Image fade, progressFill;
        private Text progressText, extractTitle;
        private CanvasGroup group;
        public bool ControlsOpen { get; private set; }
        private readonly Color cyan = new Color(0.23f, 0.9f, 1f);
        private readonly Color green = new Color(0.3f, 1f, 0.62f);
        private readonly Color muted = new Color(0.65f, 0.75f, 0.8f);

        public void Build(GameFlowController owner)
        {
            flow = owner;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Mission Flow Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            root = go.GetComponent<RectTransform>();
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("Mission EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(transform, false);
        }

        public void Show(MissionState state)
        {
            ControlsOpen = false;
            if (screen != null) { screen.SetActive(false); Destroy(screen); }
            screen = new GameObject("Screen " + state, typeof(RectTransform), typeof(CanvasGroup));
            screen.transform.SetParent(root, false);
            Stretch(screen.GetComponent<RectTransform>());
            group = screen.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            if (state == MissionState.Playing) { screen.SetActive(false); return; }
            var shade = Panel("Backdrop", screen.transform, new Color(0.015f, 0.035f, 0.055f, state == MissionState.Extracting ? 0.2f : 0.83f));
            Stretch(shade.rectTransform);
            if (state == MissionState.Extracting) { BuildExtraction(); return; }
            var content = Panel("Command Panel", screen.transform, new Color(0.025f, 0.055f, 0.075f, 0.94f));
            Place(content.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1180, 860), Vector2.zero);
            var line = Panel("Status rule", content.transform, cyan);
            Place(line.rectTransform, new Vector2(0.5f, 1), new Vector2(1080, 3), new Vector2(0, -46));
            Label(content.transform, "ISOLATED RESEARCH FACILITY  /  EMERGENCY NETWORK", 19, muted, 0, 348, 1080, 35);
            if (state == MissionState.MainMenu)
            {
                Label(content.transform, "FACILITY ESCAPE", 72, Color.white, 0, 235, 1080, 100, FontStyle.Bold);
                Label(content.transform, "E M E R G E N C Y   P R O T O C O L", 24, cyan, 0, 155, 1080, 45);
                Label(content.transform, "EXTRACTION OFFLINE  /  SECURITY HOSTILE", 20, new Color(1f, 0.65f, 0.3f), 0, 73, 1080, 40);
                Button(content.transform, "START MISSION", -32, flow.StartMission, true);
                Button(content.transform, "CONTROLS", -114, flow.ShowControls);
                Button(content.transform, "QUIT GAME", -196, flow.QuitGame);
                Label(content.transform, "Recover the backup cells. Restore emergency power. Escape.", 23, muted, 0, -325, 1080, 50);
            }
            else if (state == MissionState.Briefing)
            {
                Label(content.transform, "EMERGENCY TRANSMISSION", 43, Color.white, 0, 255, 1080, 65, FontStyle.Bold);
                Label(content.transform, "Emergency protocol activated. A critical system failure has disabled the facility's extraction network. Autonomous security units are operating in hostile mode. Recover three backup power cells, restore the extraction terminal and escape.", 27, Color.white, 0, 110, 1010, 180);
                Label(content.transform, Controls, 23, cyan, 0, -78, 1000, 150);
                Button(content.transform, "BEGIN MISSION", -232, flow.BeginMission, true);
                Label(content.transform, "Hunter / tracking     Flanker / tactical response\nSentinel / area defense     Repairer / maintenance support", 19, muted, 0, -335, 1020, 68);
            }
            else if (state == MissionState.Paused)
            {
                Label(content.transform, "MISSION PAUSED", 52, Color.white, 0, 225, 1080, 80, FontStyle.Bold);
                Label(content.transform, "SECURITY ACTIVITY SUSPENDED", 21, cyan, 0, 145, 1080, 45);
                Button(content.transform, "RESUME", 35, flow.Resume, true);
                Button(content.transform, "RESTART MISSION", -47, flow.RestartMission);
                Button(content.transform, "CONTROLS", -129, flow.ShowControls);
                Button(content.transform, "MAIN MENU", -211, flow.ReturnToMainMenu);
            }
            else BuildResult(content.transform, state == MissionState.Victory);
            Label(content.transform, "FACILITY ESCAPE  //  EMERGENCY PROTOCOL", 16, muted, 0, -397, 1080, 25);
        }

        private const string Controls = "WASD  Move       MOUSE  Look       LEFT MOUSE  Fire\nE  Interact       G  Throw noise device       ESC  Pause\nWalk into power cells and health supplies to collect them.";
        public void OpenControls()
        {
            ControlsOpen = true;
            if (screen != null) { screen.SetActive(false); Destroy(screen); }
            screen = new GameObject("Controls", typeof(RectTransform));
            screen.transform.SetParent(root, false);
            Stretch(screen.GetComponent<RectTransform>());
            var back = Panel("Controls backdrop", screen.transform, new Color(0.015f, 0.035f, 0.055f, 0.96f));
            Stretch(back.rectTransform);
            Label(screen.transform, "FIELD CONTROLS", 52, Color.white, 0, 220, 1100, 80, FontStyle.Bold);
            Label(screen.transform, Controls, 29, cyan, 0, 35, 1300, 200);
            Label(screen.transform, "Gunshots and thrown devices attract nearby security units.\nCollect all three cells before activating the extraction terminal.", 25, muted, 0, -130, 1200, 100);
            Button(screen.transform, "BACK", -260, CloseControls, true);
        }
        public void CloseControls() { ControlsOpen = false; Show(flow.State); }

        private void BuildResult(Transform parent, bool won)
        {
            Label(parent, won ? "MISSION COMPLETE" : "MISSION FAILED", 62, won ? green : new Color(1f, 0.55f, 0.35f), 0, 230, 1080, 90, FontStyle.Bold);
            Label(parent, won ? "EMERGENCY EXTRACTION SUCCESSFUL" : "SECURITY SYSTEM OVERWHELMED YOU", 25, Color.white, 0, 145, 1080, 55);
            Label(parent, "BACKUP CELLS  " + flow.Cells + " / 3     |     SECURITY UNITS ELIMINATED  " + flow.EnemiesEliminated +
                "\nREMAINING HEALTH  " + flow.RemainingHealth + "     |     MISSION TIME  " + flow.MissionTime,
                25, muted, 0, 15, 1040, 110);
            Button(parent, won ? "PLAY AGAIN" : "RETRY MISSION", -130, flow.RestartMission, true);
            Button(parent, "MAIN MENU", -220, flow.ReturnToMainMenu);
        }

        private void BuildExtraction()
        {
            var p = Panel("Extraction status", screen.transform, new Color(0.02f, 0.06f, 0.08f, 0.94f));
            Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1100, 330), new Vector2(0, -130));
            extractTitle = Label(p.transform, "RESTORING EMERGENCY POWER", 38, cyan, 0, 85, 1040, 70, FontStyle.Bold);
            Label(p.transform, "Restoring extraction systems...", 24, Color.white, 0, 20, 1040, 45);
            var track = Panel("Power transfer", p.transform, new Color(0.15f, 0.25f, 0.3f));
            Place(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(880, 12), new Vector2(0, -35));
            progressFill = Panel("Progress", track.transform, cyan);
            Stretch(progressFill.rectTransform);
            progressText = Label(p.transform, "0%", 28, Color.white, 0, -95, 1040, 50);
            fade = Panel("Extraction fade", screen.transform, new Color(0, 0, 0, 0));
            Stretch(fade.rectTransform);
        }

        public void ExtractionProgress(float progress, float fadeAmount)
        {
            if (progressFill == null) return;
            progressFill.rectTransform.anchorMax = new Vector2(progress, 1);
            progressText.text = Mathf.RoundToInt(progress * 100) + "%";
            if (progress >= 1) { extractTitle.text = "EXTRACTION AUTHORIZED"; extractTitle.color = green; }
            fade.color = new Color(0, 0, 0, fadeAmount);
        }

        private void Update() { if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, 1, Time.unscaledDeltaTime * 5); }
        private Image Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = true;
            return image;
        }
        private Text Label(Transform parent, string value, int size, Color color, float x, float y, float w, float h, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(value.Split('\n')[0], typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.fontStyle = style;
            t.color = color; t.text = value; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(w, h), new Vector2(x, y));
            return t;
        }
        private void Button(Transform parent, string text, float y, UnityAction action, bool primary = false)
        {
            var p = Panel(text, parent, primary ? new Color(0.08f, 0.29f, 0.35f) : new Color(0.07f, 0.12f, 0.16f));
            Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(550, 64), new Vector2(0, y));
            var button = p.gameObject.AddComponent<Button>();
            button.targetGraphic = p;
            var colors = button.colors; colors.highlightedColor = new Color(0.5f, 0.9f, 1); colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(action);
            Label(p.transform, text, 24, Color.white, 0, 0, 510, 55, FontStyle.Bold);
            var stripe = Panel("Action marker", p.transform, primary ? cyan : muted);
            Place(stripe.rectTransform, new Vector2(0, 0.5f), new Vector2(3, 64), Vector2.zero);
        }
        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
        { rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos; }
        private static void Stretch(RectTransform rt)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    }
}
