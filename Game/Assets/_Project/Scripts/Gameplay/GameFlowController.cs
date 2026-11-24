using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using FacilityEscape.Combat;
using FacilityEscape.Feedback;
using FacilityEscape.Player;

namespace FacilityEscape.Gameplay
{
    public enum MissionState { MainMenu, Briefing, Playing, Paused, Extracting, Victory, Defeat }

    // Scene-local coordinator. Reloading this scene is the single authoritative mission reset.
    [DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        public static GameFlowController Instance { get; private set; }
        public static bool GameplayAllowed => Instance == null || Instance.State == MissionState.Playing;
        private static MissionState nextLoadState = MissionState.MainMenu;

        [SerializeField] private FirstPersonController player;
        [SerializeField] private Health playerHealth;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private GameHud hud;
        [SerializeField] private PowerCellManager powerCells;
        private MissionFlowView view;
        private AudioSource feedback, ambience;
        private readonly List<Health> enemies = new List<Health>();
        private readonly List<Light> extractionLights = new List<Light>();
        private bool loading;
        private GameObject weapon;
        public MissionState State { get; private set; } = MissionState.MainMenu;
        public float MissionSeconds { get; private set; }
        public int Cells => powerCells != null ? powerCells.CollectedCount : 0;
        public int RemainingHealth => playerHealth != null ? playerHealth.CurrentHealth : 0;
        public int EnemiesEliminated { get { int n = 0; foreach (var h in enemies) if (h != null && h.IsDestroyed) n++; return n; } }
        public string MissionTime => System.TimeSpan.FromSeconds(MissionSeconds).ToString(@"mm\:ss");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; nextLoadState = MissionState.MainMenu; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        private void Start()
        {
            if (player == null) player = FindFirstObjectByType<FirstPersonController>();
            if (playerHealth == null && player != null) playerHealth = player.GetComponent<Health>();
            if (playerCamera == null && player != null) playerCamera = player.GetComponentInChildren<Camera>(true);
            if (hud == null) hud = FindFirstObjectByType<GameHud>();
            if (powerCells == null) powerCells = FindFirstObjectByType<PowerCellManager>();
            var gun = FindFirstObjectByType<PulseGun>();
            if (gun != null) weapon = gun.gameObject;
            foreach (var agent in FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))
            {
                var health = agent.GetComponent<Health>();
                if (health != null && health != playerHealth) enemies.Add(health);
            }
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type != LightType.Directional && light.transform.position.x < -37f &&
                    Mathf.Abs(light.transform.position.z) < 4.5f) extractionLights.Add(light);
            feedback = gameObject.AddComponent<AudioSource>();
            feedback.playOnAwake = false;
            feedback.spatialBlend = 0f;
            feedback.ignoreListenerPause = true;
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.playOnAwake = false;
            ambience.spatialBlend = 0f;
            ambience.ignoreListenerPause = true;
            ambience.loop = true;
            ambience.volume = 0.04f;
            ambience.clip = CreateAmbience();
            view = gameObject.AddComponent<MissionFlowView>();
            view.Build(this);
            var first = nextLoadState;
            nextLoadState = MissionState.MainMenu;
            SetState(first);
        }

        private void Update()
        {
            if (State == MissionState.Playing) MissionSeconds += Time.deltaTime;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (view != null && view.ControlsOpen) view.CloseControls();
                else if (State == MissionState.Playing) Pause();
                else if (State == MissionState.Paused) Resume();
            }
        }

        public void StartMission() { if (State == MissionState.MainMenu) { SetState(MissionState.Briefing); Cue(SfxId.Alert); } }
        public void BeginMission() { if (State == MissionState.Briefing) SetState(MissionState.Playing); }
        public void Pause() { if (State == MissionState.Playing) SetState(MissionState.Paused); }
        public void Resume() { if (State == MissionState.Paused) SetState(MissionState.Playing); }
        public void RestartMission() => Reload(MissionState.Playing);
        public void ReturnToMainMenu() => Reload(MissionState.MainMenu);
        public void ShowControls() { if (view != null) view.OpenControls(); }
        public void QuitGame()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Application.Quit();
#if UNITY_EDITOR
            Debug.Log("Quit requested. The standalone application closes here.");
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Reload(MissionState destination)
        {
            if (loading) return;
            loading = true;
            nextLoadState = destination;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void SetState(MissionState state)
        {
            State = state;
            bool playing = state == MissionState.Playing;
            Time.timeScale = playing ? 1f : 0f;
            AudioListener.pause = !playing;
            if (player != null) player.enabled = playing;
            if (weapon != null) weapon.SetActive(playing);
            // FirstPersonController.OnDisable restores the old camera; explicitly keep the live view.
            if (sceneCamera != null) sceneCamera.enabled = false;
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                var listener = playerCamera.GetComponent<AudioListener>();
                foreach (var other in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    other.enabled = other == listener;
            }
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
            if (hud != null) hud.SetVisible(playing);
            if (view != null) view.Show(state);
            if (ambience != null)
            {
                bool menu = state == MissionState.MainMenu || state == MissionState.Briefing;
                if (menu && !ambience.isPlaying) ambience.Play();
                else if (!menu) ambience.Stop();
            }
            // Terminal/death are final for this session; stop AI callbacks and residual attack visuals.
            if (state == MissionState.Extracting || state == MissionState.Victory || state == MissionState.Defeat)
            {
                for (int pass = 0; pass < 2; pass++)
                    foreach (var agent in FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))
                        foreach (var script in agent.GetComponents<MonoBehaviour>())
                            if (script != null && script.GetType().Namespace != null &&
                                script.GetType().Namespace.StartsWith("FacilityEscape.AI")) script.enabled = false;
                foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                    if (source != feedback && source != ambience) source.Stop();
                foreach (var line in FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)) line.enabled = false;
            }
        }

        public static bool HandlePlayerDefeat(Health health)
        {
            var flow = Instance;
            if (flow == null || flow.playerHealth != health) return false;
            if (flow.State == MissionState.Playing)
            {
                flow.SetState(MissionState.Defeat);
                flow.Cue(SfxId.Death);
                Debug.Log("PLAYER DEFEATED");
            }
            return true; // Keep Player and its camera active, with movement disabled.
        }

        public bool BeginExtraction(ExitTerminal terminal)
        {
            if (State != MissionState.Playing || terminal == null || !terminal.IsUnlocked ||
                Cells != PowerCellManager.TotalCells || RemainingHealth <= 0) return false;
            SetState(MissionState.Extracting);
            Cue(SfxId.DoorOpen);
            StartCoroutine(Extract(terminal));
            return true;
        }

        private IEnumerator Extract(ExitTerminal terminal)
        {
            const float duration = 5f;
            float elapsed = 0f;
            bool authorized = false;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / 3.8f);
                terminal.ShowExtractionProgress(progress);
                foreach (var light in extractionLights)
                    if (light != null) light.color = Color.Lerp(new Color(0.35f, 0.8f, 0.9f), new Color(0.1f, 1f, 0.5f), progress);
                view.ExtractionProgress(progress, Mathf.InverseLerp(4.2f, duration, elapsed));
                if (progress >= 1f && !authorized) { authorized = true; Cue(SfxId.PowerCellPickup); }
                yield return null;
            }
            SetState(MissionState.Victory);
            Cue(SfxId.PowerCellPickup);
            Debug.Log("FACILITY ESCAPE SUCCESS");
        }

        private void Cue(SfxId id) { if (feedback != null) feedback.PlayOneShot(Sfx.GetClip(id), 0.45f); }

        private static AudioClip CreateAmbience()
        {
            const int rate = 22050;
            var samples = new float[rate * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                samples[i] = Mathf.Sin(t * Mathf.PI * 2f * 55f) * 0.4f + Mathf.Sin(t * Mathf.PI * 2f * 110f) * 0.08f;
            }
            var clip = AudioClip.Create("Facility ventilation", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (ambience != null && ambience.clip != null) Destroy(ambience.clip);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
