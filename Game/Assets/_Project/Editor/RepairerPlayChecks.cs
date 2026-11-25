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

    static RepairerPlayChecks() => EditorApplication.update += Tick;

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
        var camera = Camera.main;
        camera.transform.LookAt(hitboxes[0].bounds.center);
        Physics.SyncTransforms();
        int initial = hp.CurrentHealth;
        Require(gun.TryFire() && hp.CurrentHealth < initial, "center-screen PulseGun hit reduces Repairer HP");
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

        yield return Until("initial gunfire expires", () => robot.ThreatSeconds <= 0f);
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
    }

    private static void Finish(bool passed)
    {
        EditorApplication.UnlockReloadAssemblies();
        SessionState.SetBool(Key, false);
        condition = null;
        sequence = null;
        Debug.Log("Repairer suite " + (passed ? "PASS" : "FAIL"));
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
