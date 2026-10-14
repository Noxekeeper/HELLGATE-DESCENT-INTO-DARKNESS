using System.Collections.Generic;
using BepInEx.Configuration;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Pauses Spine on HellGate spawns far outside the camera ([Performance] CullOffscreenSpine).
/// Every SkeletonAnimation rebuilds its animation and mesh on the CPU each frame even off-screen;
/// packs keep ~100 of them alive with only a handful visible. AI and physics live on EnemyDate /
/// Rigidbody2D, so a paused skeleton only freezes visuals and animation events of far objects.
/// Only components this culler disabled are re-enabled.
/// </summary>
internal sealed class SpawnSpineCuller : MonoBehaviour
{
    private const float PassInterval = 0.2f;
    // In half-screens from the camera centre; hysteresis avoids flicker at the edge.
    private const float CullDistance = 2f;
    private const float WakeDistance = 1.5f;

    private sealed class Entry
    {
        internal readonly List<SkeletonAnimation> Paused = new List<SkeletonAnimation>();
        internal bool Culled;
    }

    private static readonly Dictionary<SpawnManagedInstance, Entry> Entries =
        new Dictionary<SpawnManagedInstance, Entry>();
    private static readonly List<SpawnManagedInstance> Snapshot = new List<SpawnManagedInstance>();

    private static ConfigEntry<bool> enabledConfig;
    private static SpawnSpineCuller instance;
    private static int culledCount;

    private float nextPass;
    private bool loggedCamera;

    internal static int CulledCount => culledCount;

    private static bool IsEnabled => enabledConfig == null || enabledConfig.Value;

    internal static void Initialize(ConfigFile config)
    {
        enabledConfig = config.Bind(
            "Performance",
            "CullOffscreenSpine",
            true,
            "Pause Spine animation of HellGate-spawned enemies, hostages and decor far outside the camera (big FPS gain on dense packs).");
        enabledConfig.SettingChanged += (_, __) =>
        {
            if (!IsEnabled)
                WakeAll();
        };

        if (instance == null)
        {
            GameObject go = new GameObject("HellGate_SpawnSpineCuller");
            Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<SpawnSpineCuller>();
        }
    }

    internal static void Register(SpawnManagedInstance managed)
    {
        if (managed != null && !Entries.ContainsKey(managed))
            Entries[managed] = new Entry();
    }

    internal static void Unregister(SpawnManagedInstance managed)
    {
        if (managed == null || !Entries.TryGetValue(managed, out Entry entry))
            return;

        Wake(entry);
        Entries.Remove(managed);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextPass)
            return;
        nextPass = Time.unscaledTime + PassInterval;

        if (!IsEnabled || Entries.Count == 0)
            return;

        UnityEngine.Camera cam = UnityEngine.Camera.main;
        if (cam == null)
            return;

        if (!loggedCamera)
        {
            loggedCamera = true;
            Plugin.Log?.LogInfo(
                $"[SPINE CULL] active: camera={cam.name} ortho={cam.orthographic} tracked={Entries.Count}");
        }

        Snapshot.Clear();
        Snapshot.AddRange(Entries.Keys);
        for (int i = 0; i < Snapshot.Count; i++)
        {
            SpawnManagedInstance managed = Snapshot[i];
            if (managed == null)
            {
                Entries.Remove(managed);
                continue;
            }

            Entry entry = Entries[managed];
            Vector3 v = cam.WorldToViewportPoint(managed.transform.position);
            float d = v.z <= 0f
                ? float.MaxValue
                : Mathf.Max(Mathf.Abs(v.x - 0.5f), Mathf.Abs(v.y - 0.5f)) * 2f;

            if (!entry.Culled && d > CullDistance)
                Cull(managed, entry);
            else if (entry.Culled && d < WakeDistance)
                Wake(entry);
        }
    }

    private static void Cull(SpawnManagedInstance managed, Entry entry)
    {
        SkeletonAnimation[] spines = managed.GetComponentsInChildren<SkeletonAnimation>(true);
        entry.Paused.Clear();
        for (int i = 0; i < spines.Length; i++)
        {
            SkeletonAnimation spine = spines[i];
            if (spine == null || !spine.enabled)
                continue;
            spine.enabled = false;
            entry.Paused.Add(spine);
        }

        entry.Culled = true;
        culledCount++;
    }

    private static void Wake(Entry entry)
    {
        if (!entry.Culled)
            return;

        for (int i = 0; i < entry.Paused.Count; i++)
        {
            if (entry.Paused[i] != null)
                entry.Paused[i].enabled = true;
        }

        entry.Paused.Clear();
        entry.Culled = false;
        culledCount--;
    }

    private static void WakeAll()
    {
        foreach (Entry entry in Entries.Values)
            Wake(entry);
    }
}
