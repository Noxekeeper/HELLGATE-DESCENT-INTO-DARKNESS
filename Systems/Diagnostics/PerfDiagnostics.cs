using System;
using BepInEx.Configuration;
using NoREroMod.Systems.Spawn;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Diagnostics;

/// <summary>
/// Dev frame-time log ([Diagnostics] PerfLog): every <see cref="ReportInterval"/> seconds writes
/// average / worst frame, hitch counts, GC collections, Mono heap and live object counts.
/// </summary>
internal sealed class PerfDiagnostics : MonoBehaviour
{
    private const float ReportInterval = 5f;
    private const float HitchMs = 33.4f;
    private const float BigHitchMs = 50f;

    private static ConfigEntry<bool> enabledConfig;
    private static PerfDiagnostics instance;

    private float windowStart;
    private int frames;
    private float sumMs;
    private float worstMs;
    private int hitches;
    private int bigHitches;
    private int gcAtWindowStart;

    internal static void Initialize(ConfigFile config)
    {
        enabledConfig = config.Bind(
            "Diagnostics",
            "PerfLog",
            false,
            "Dev: every 5 s log average / worst frame time, hitches, GC collections and enemy / Spine / HellGate spawn counts.");
        enabledConfig.SettingChanged += (_, __) => Sync();
        PerfMethodProfiler.Initialize(config);
        Sync();
    }

    internal static bool IsEnabled => enabledConfig != null && enabledConfig.Value;
    internal static bool HasReported { get; private set; }

    private static void Sync()
    {
        bool on = enabledConfig != null && enabledConfig.Value;
        if (on && instance == null)
        {
            GameObject go = new GameObject("HellGate_PerfDiagnostics");
            Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<PerfDiagnostics>();
        }
        else if (!on && instance != null)
        {
            Object.Destroy(instance.gameObject);
            instance = null;
        }

        PerfMethodProfiler.Sync();
    }

    private void OnEnable()
    {
        ResetWindow();
    }

    private void ResetWindow()
    {
        windowStart = Time.realtimeSinceStartup;
        frames = 0;
        sumMs = 0f;
        worstMs = 0f;
        hitches = 0;
        bigHitches = 0;
        gcAtWindowStart = GC.CollectionCount(0);
    }

    private void Update()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        frames++;
        sumMs += ms;
        if (ms > worstMs)
            worstMs = ms;
        if (ms > HitchMs)
            hitches++;
        if (ms > BigHitchMs)
            bigHitches++;

        if (Time.realtimeSinceStartup - windowStart < ReportInterval)
            return;

        Report();
        ResetWindow();
    }

    private void Report()
    {
        float avg = frames > 0 ? sumMs / frames : 0f;
        float fps = avg > 0f ? 1000f / avg : 0f;
        int gc = GC.CollectionCount(0) - gcAtWindowStart;
        float heapMb = GC.GetTotalMemory(false) / (1024f * 1024f);

        int enemies = Object.FindObjectsOfType<EnemyDate>().Length;
        SkeletonAnimation[] spines = Object.FindObjectsOfType<SkeletonAnimation>();
        int visibleSpines = 0;
        for (int i = 0; i < spines.Length; i++)
        {
            Renderer r = spines[i] != null ? spines[i].GetComponent<Renderer>() : null;
            if (r != null && r.isVisible)
                visibleSpines++;
        }

        int hgSpawns = Object.FindObjectsOfType<SpawnManagedInstance>().Length;
        int rotLocks = Object.FindObjectsOfType<SpawnFixedRotation>().Length;
        int facingLocks = Object.FindObjectsOfType<SpawnFixedFacing>().Length;

        string zone = HellGateLocationSpawnRefresh.GetActiveGameplayZone();
        if (string.IsNullOrEmpty(zone))
            zone = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        Plugin.Log?.LogInfo(
            $"[PERF] zone={zone} fps={fps:F0} avg={avg:F1}ms worst={worstMs:F0}ms "
            + $"hitch>33ms={hitches} >50ms={bigHitches} gc={gc} heap={heapMb:F0}MB | "
            + $"enemies={enemies} spine={spines.Length} (visible {visibleSpines}) hgSpawns={hgSpawns} "
            + $"spineCulled={SpawnSpineCuller.CulledCount} rotLock={rotLocks} facingLock={facingLocks} timeScale={Time.timeScale:F2}");

        string top = PerfMethodProfiler.BuildReportAndReset(frames);
        if (top != null)
            Plugin.Log?.LogInfo(top);

        HasReported = true;
        PerfMethodProfiler.Sync();
    }
}
