using System;
using System.Collections;
using System.Linq;
using FacilityEscape.AI.Common;
using FacilityEscape.AI.Repairer;
using FacilityEscape.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

// Runs the same gameplay sequence from the Tools menu and the batch-mode entry point.
[InitializeOnLoad]
public static class RepairerPlayChecks
{
    private const string Key = "FacilityEscape.RepairerPlayChecks";
    private const string Scene = "Assets/_Project/Scenes/RepairerDemo.unity";
    private static IEnumerator sequence;
    private static Func<bool> condition;
    private static double deadline;
    private static string expectation;
    private static InputDevice[] suspendedDevices = Array.Empty<InputDevice>();

    static RepairerPlayChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // A held menu click must not fire the gun or move the camera during the suite.
                suspendedDevices = InputSystem.devices.Where(device => device.enabled &&
                    (device is Mouse || device is Keyboard)).ToArray();
                foreach (var device in suspendedDevices) InputSystem.DisableDevice(device, keepSendingEvents: true);
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
                ClearRun();
        };
    }

    [MenuItem("Tools/Repairer/Run showcase checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(Scene);
        EditorApplication.LockReloadAssemblies();
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || Time.frameCount < 4) return;
        try
        {
            if (condition != null)
            {
                if (!condition())
                {
                    if (EditorApplication.timeSinceStartup >= deadline) throw new Exception(expectation);
                    return;
                }
                Debug.Log("Repairer check passed: " + expectation);
                condition = null;
            }
            sequence ??= Exercise();
            if (!sequence.MoveNext()) Finish(true);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Finish(false);
        }
    }

    private static object Until(string label, Func<bool> predicate, int seconds = 20)
    {
        expectation = label;
        condition = predicate;
        deadline = EditorApplication.timeSinceStartup + seconds;
        return null;
    }

    private static void Require(bool value, string label)
    {
        if (!value) throw new Exception(label);
        Debug.Log("Repairer check passed: " + label);
    }

    private static IEnumerator Exercise()
    {
        var demo = Object.FindFirstObjectByType<RepairerDemo>();
        var robot = Object.FindFirstObjectByType<RepairerAI>();
        var agent = robot.GetComponent<NavMeshAgent>();
        var shoulder = robot.GetComponentsInChildren<Transform>().Single(t => t.name == "Repairer_Arm");
        var idle = shoulder.localRotation;
        var hp = robot.GetComponent<Health>();
        var sentinel = GameObject.Find("Sentinel").GetComponent<Health>();
        var line = robot.GetComponentInChildren<LineRenderer>(true);
        Require(agent.isOnNavMesh, "demo agent matches its bake");
        var hitboxes = robot.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
        Require(hitboxes.Length == 1 && hitboxes[0].name == "Body", "only Body receives solid hits");
        var gun = Object.FindFirstObjectByType<PulseGun>();
        var camera = (Camera)new SerializedObject(gun).FindProperty("playerCamera").objectReferenceValue;
        Require(camera && camera.isActiveAndEnabled, "PulseGun references an active camera");
        camera.transform.LookAt(hitboxes[0].bounds.center);
        Physics.SyncTransforms();
        int initial = hp.CurrentHealth;
        int shotsBefore = gun.ShotsFired;
        bool fired = gun.TryFire();
        Require(fired && gun.ShotsFired == shotsBefore + 1,
            $"controlled PulseGun shot fired (count {shotsBefore} → {gun.ShotsFired}, " +
            $"gun enabled {gun.isActiveAndEnabled}, gameplay allowed {FacilityEscape.Gameplay.GameFlowController.GameplayAllowed})");
        Require(hp.CurrentHealth < initial,
            $"center-screen PulseGun hit reduces Repairer HP (HP {initial} → {hp.CurrentHealth}, " +
            $"camera {camera.name}, target {hitboxes[0].name} at {hitboxes[0].bounds.center})");
        var block = new MaterialPropertyBlock();
        bool Flashed()
        {
            foreach (var surface in robot.GetComponentsInChildren<MeshRenderer>())
            {
                surface.GetPropertyBlock(block);
                if (block.HasColor(Shader.PropertyToID("_BaseColor"))) return true;
            }
            return false;
        }
        yield return Until("damage flash appears", Flashed);
        yield return Until("damage flash clears", () => !Flashed());
        Require(hp.Heal(int.MaxValue) && hp.CurrentHealth == hp.MaxHealth && !hp.Heal(1), "healing caps at maximum");

        yield return Until("initial gunfire expires", () =>
        {
            if (gun.ShotsFired != shotsBefore + 1)
                throw new Exception($"Unexpected additional PulseGun fire while waiting for threat expiry " +
                    $"(expected {shotsBefore + 1} total, observed {gun.ShotsFired}, threat {robot.ThreatSeconds:F2}s).");
            return robot.ThreatSeconds <= 0f;
        });
        demo.ToggleSentinelReachability();
        demo.DamageSentinel();
        int damaged = sentinel.CurrentHealth;
        float blockedUntil = Time.time + 0.8f;
        yield return Until("blocked route is rejected", () => Time.time > blockedUntil &&
            robot.RepairTarget == null && sentinel.CurrentHealth == damaged && robot.Status?.Contains("reachable") == true);
        demo.ToggleSentinelReachability();
        yield return Until("reconnected route delivers healing", () => sentinel.CurrentHealth > damaged && line.enabled);

        yield return Until("shoulder moves and the beam follows its emitter", () =>
            line.enabled && Quaternion.Angle(idle, shoulder.localRotation) > 5f &&
            line.transform.IsChildOf(shoulder) && Vector3.Distance(line.GetPosition(0), line.transform.position) < 0.03f);

        var rival = new GameObject("Competing test ally");
        Vector3 away = (robot.transform.position - sentinel.transform.position).normalized;
        rival.transform.position = robot.transform.position + away * 1.25f;
        var hull = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        hull.transform.SetParent(rival.transform, false);
        hull.transform.localPosition = Vector3.up;
        Physics.SyncTransforms();
        var rivalHp = rival.AddComponent<Health>();
        rival.AddComponent<RepairableAlly>();
        rivalHp.TakeDamage(Mathf.Clamp(sentinel.MaxHealth - sentinel.CurrentHealth + 1, 1, 90));
        float observeUntil = Time.time + 0.35f;
        yield return Until("eligible incumbent retained", () => Time.time > observeUntil && robot.RepairTarget == sentinel);
        rivalHp.TakeDamage(rivalHp.CurrentHealth - 1);
        yield return Until("clearly more urgent ally selected", () => robot.RepairTarget == rivalHp);
        sentinel.TakeDamage(Mathf.Min(25, sentinel.CurrentHealth - 1));
        rival.SetActive(false);
        yield return Until("invalid target replaced", () => robot.RepairTarget == sentinel && line.enabled);
        int interrupted = sentinel.CurrentHealth;
        var shot = robot.transform.position;
        SoundEvents.Emit(shot, 25f, isGunfire: true);
        yield return Until("gunfire interrupts beam and causes retreat", () =>
            robot.SelectedAction == RepairerAction.Escape && !line.enabled &&
            Vector3.Distance(shot, robot.transform.position) > 0.35f);
        yield return Until("shoulder retracts on retreat", () => Quaternion.Angle(idle, shoulder.localRotation) < 0.5f);
        Require(sentinel.CurrentHealth == interrupted, "no healing while retreating");
        yield return Until("repair resumes after threat expires", () => sentinel.CurrentHealth > interrupted && line.enabled);
        yield return Until("full ally releases beam", () => sentinel.CurrentHealth == sentinel.MaxHealth && !line.enabled);
        var home = GameObject.Find("Safe point").transform;
        yield return Until("robot returns home", () => Vector3.Distance(robot.transform.position, home.position) < 0.4f);
        Require(RepairerScoring.RetreatMarker(6f, 4f, true) > RepairerScoring.RetreatMarker(6f, 4f, false),
            "cover improves otherwise equal retreat candidates");

        var previous = demo;
        demo.Restart(RepairerDemo.Scenario.Competing);
        yield return Until("competing scenario loads in Play Mode", () => !previous &&
            Object.FindFirstObjectByType<RepairerDemo>() && GameObject.Find("Maintenance drone"));
        demo = Object.FindFirstObjectByType<RepairerDemo>();
        var contender = GameObject.Find("Maintenance drone").GetComponent<Health>();
        int before = contender.CurrentHealth;
        demo.DamageCompetitor();
        Require(contender.CurrentHealth < before, "competing ally damage control");

        previous = demo;
        demo.Restart(RepairerDemo.Scenario.Blocked);
        yield return Until("blocked scenario loads in Play Mode", () => !previous &&
            GameObject.Find("Retractable maintenance barrier"));
        demo = Object.FindFirstObjectByType<RepairerDemo>();
        robot = Object.FindFirstObjectByType<RepairerAI>();
        sentinel = GameObject.Find("Sentinel").GetComponent<Health>();
        damaged = sentinel.CurrentHealth;
        float settle = Time.time + 1f;
        yield return Until("carved gate rejects repair while closed", () => Time.time > settle &&
            robot.RepairTarget == null && sentinel.CurrentHealth == damaged && robot.Status?.Contains("reachable") == true);
        demo.ToggleBarrier();
        yield return Until("opening the gate restores healing", () => sentinel.CurrentHealth > damaged);
        previous = demo;
        demo.Restart(RepairerDemo.Scenario.Single);
        yield return Until("reset clears scenario objects", () => !previous &&
            Object.FindFirstObjectByType<RepairerDemo>() && !GameObject.Find("Retractable maintenance barrier"));
        robot = Object.FindFirstObjectByType<RepairerAI>();
        agent = robot.GetComponent<NavMeshAgent>();
        var north = GameObject.Find("Retreat marker").transform;
        home = GameObject.Find("Safe point").transform;
        SoundEvents.Emit(north.position, 30f, isGunfire: true);
        yield return Until("shot from north selects southern refuge or holds if both are closer", () =>
            robot.ThreatSeconds > 0 && robot.SelectedAction == RepairerAction.Hold);
        // Stand between the two refuges so each source has one escape destination farther away.
        Require(agent.Warp((north.position + home.position) * 0.5f), "midpoint is on the navigation mesh");
        SoundEvents.Emit(north.position, 30f, isGunfire: true);
        yield return Until("northern shot selects home-side refuge", () =>
            robot.SelectedAction == RepairerAction.Escape && Vector3.Distance(agent.destination, home.position) < 0.6f);
        SoundEvents.Emit(home.position, 30f, isGunfire: true);
        yield return Until("southern shot selects northern refuge", () =>
            robot.SelectedAction == RepairerAction.Escape && Vector3.Distance(agent.destination, north.position) < 0.6f);
        north.gameObject.SetActive(false);
        home.gameObject.SetActive(false);
        SoundEvents.Emit(robot.transform.position, 30f, isGunfire: true);
        yield return Until("threat without a reachable refuge holds fire and healing", () =>
            robot.SelectedAction == RepairerAction.Hold && robot.ActiveRepairTarget == null);
    }

    private static void ClearRun()
    {
        foreach (var device in suspendedDevices)
            if (device.added) InputSystem.EnableDevice(device);
        suspendedDevices = Array.Empty<InputDevice>();
        EditorApplication.UnlockReloadAssemblies();
        SessionState.SetBool(Key, false);
        condition = null;
        sequence = null;
    }

    private static void Finish(bool passed)
    {
        ClearRun();
        Debug.Log("Repairer suite " + (passed ? "PASS" : "FAIL"));
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
