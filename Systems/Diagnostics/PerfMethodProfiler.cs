using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using Spine.Unity;

namespace NoREroMod.Systems.Diagnostics;

/// <summary>
/// Dev ([Diagnostics] PerfProfileMethods): IL-instruments every Harmony-patched method, every patch
/// method attached to it (all plugins) and Spine Update/LateUpdate with a begin/end timer;
/// <see cref="PerfDiagnostics"/> prints the top entries per report window. Patched originals are
/// timed without their patches — patches show up as their own rows. Adds overhead.
/// </summary>
internal static class PerfMethodProfiler
{
    private const string HarmonyId = "HellGate.PerfProbe";
    private const int TopCount = 15;
    private const int MaxIds = 8192;

    private static readonly long[] Ticks = new long[MaxIds];
    private static readonly int[] Calls = new int[MaxIds];
    private static readonly int[] Depth = new int[MaxIds];
    private static readonly long[] Start = new long[MaxIds];
    private static readonly List<string> Names = new List<string>();
    private static readonly Dictionary<MethodBase, int> IdByMethod = new Dictionary<MethodBase, int>();

    private static ConfigEntry<bool> enabledConfig;
    private static Harmony harmony;

    internal static void Initialize(ConfigFile config)
    {
        enabledConfig = config.Bind(
            "Diagnostics",
            "PerfProfileMethods",
            false,
            "Dev: with PerfLog, time every Harmony-patched method and Spine updates and log the top costs every 5 s. Adds overhead; keep off for normal play.");
        enabledConfig.SettingChanged += (_, __) => Sync();
        if (enabledConfig.Value)
            Plugin.Log?.LogInfo("[PERF] method profiler armed (installs after the first PerfLog report).");
    }

    // Installed from the first PerfDiagnostics report, after every plugin has applied its patches.
    internal static void Sync()
    {
        bool on = enabledConfig != null && enabledConfig.Value && PerfDiagnostics.IsEnabled;
        if (on && harmony == null && PerfDiagnostics.HasReported)
        {
            try
            {
                Install();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[PERF] method profiler install failed: " + ex);
                if (enabledConfig != null)
                    enabledConfig.Value = false;
            }
        }
        else if (!on && harmony != null)
        {
            Uninstall();
        }
    }

    private static void Install()
    {
        harmony = new Harmony(HarmonyId);
        var transpiler = new HarmonyMethod(AccessTools.Method(typeof(PerfMethodProfiler), nameof(Transpiler)));

        var targets = new HashSet<MethodBase>();
        foreach (MethodBase original in Harmony.GetAllPatchedMethods().ToList())
        {
            targets.Add(original);
            HarmonyLib.Patches info = Harmony.GetPatchInfo(original);
            if (info == null)
                continue;
            foreach (Patch p in info.Prefixes.Concat(info.Postfixes).Concat(info.Finalizers))
            {
                if (p?.PatchMethod != null)
                    targets.Add(p.PatchMethod);
            }
        }

        AddTarget(targets, typeof(SkeletonAnimation), "Update");
        AddTarget(targets, typeof(SkeletonRenderer), "LateUpdate");

        int ok = 0;
        int failed = 0;
        foreach (MethodBase method in targets)
        {
            if (method == null || method.IsAbstract || method.ContainsGenericParameters)
                continue;
            if (method.DeclaringType == typeof(PerfMethodProfiler))
                continue;
            try
            {
                if (method.GetMethodBody() == null)
                    continue;
                harmony.Patch(method, transpiler: transpiler);
                ok++;
            }
            catch
            {
                failed++;
            }
        }

        Plugin.Log?.LogInfo($"[PERF] method profiler on: {ok} methods timed, {failed} skipped.");
    }

    private static void AddTarget(HashSet<MethodBase> targets, Type type, string name)
    {
        MethodInfo m = AccessTools.DeclaredMethod(type, name, Type.EmptyTypes);
        if (m != null)
            targets.Add(m);
    }

    private static void Uninstall()
    {
        try
        {
            harmony.UnpatchSelf();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[PERF] method profiler unpatch failed: " + ex.Message);
        }

        harmony = null;
        ResetCounters();
    }

    private static int GetId(MethodBase method)
    {
        if (IdByMethod.TryGetValue(method, out int id))
            return id;
        if (Names.Count >= MaxIds)
            return -1;

        id = Names.Count;
        string owner = method.DeclaringType != null ? method.DeclaringType.Name : "?";
        Names.Add(owner + "." + method.Name);
        IdByMethod[method] = id;
        return id;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        int id = original != null ? GetId(original) : -1;
        if (id < 0)
        {
            foreach (CodeInstruction ci in instructions)
                yield return ci;
            yield break;
        }

        MethodInfo begin = AccessTools.Method(typeof(PerfMethodProfiler), nameof(Begin));
        MethodInfo end = AccessTools.Method(typeof(PerfMethodProfiler), nameof(End));

        yield return new CodeInstruction(OpCodes.Ldc_I4, id);
        yield return new CodeInstruction(OpCodes.Call, begin);

        foreach (CodeInstruction ci in instructions)
        {
            if (ci.opcode == OpCodes.Ret)
            {
                var load = new CodeInstruction(OpCodes.Ldc_I4, id);
                load.labels.AddRange(ci.labels);
                load.blocks.AddRange(ci.blocks);
                ci.labels.Clear();
                ci.blocks.Clear();
                yield return load;
                yield return new CodeInstruction(OpCodes.Call, end);
            }

            yield return ci;
        }
    }

    public static void Begin(int id)
    {
        if (Depth[id]++ == 0)
            Start[id] = Stopwatch.GetTimestamp();
    }

    public static void End(int id)
    {
        int d = --Depth[id];
        if (d > 0)
            return;
        Depth[id] = 0;
        if (d == 0)
        {
            Ticks[id] += Stopwatch.GetTimestamp() - Start[id];
            Calls[id]++;
        }
    }

    private static void ResetCounters()
    {
        Array.Clear(Ticks, 0, Ticks.Length);
        Array.Clear(Calls, 0, Calls.Length);
        Array.Clear(Depth, 0, Depth.Length);
    }

    /// <summary>Top methods by time for the window, then resets counters.</summary>
    internal static string BuildReportAndReset(int frames)
    {
        if (harmony == null || frames <= 0)
            return null;

        int count = Names.Count;
        var order = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            if (Calls[i] > 0)
                order.Add(i);
        }

        order.Sort((a, b) => Ticks[b].CompareTo(Ticks[a]));

        double msPerTick = 1000.0 / Stopwatch.Frequency;
        var sb = new StringBuilder("[PERF] top methods (ms/frame, calls/frame):");
        for (int k = 0; k < order.Count && k < TopCount; k++)
        {
            int i = order[k];
            double ms = Ticks[i] * msPerTick / frames;
            float calls = (float)Calls[i] / frames;
            sb.Append($"\n    {ms,6:F2}  x{calls,6:F1}  {Names[i]}");
        }

        Array.Clear(Ticks, 0, Ticks.Length);
        Array.Clear(Calls, 0, Calls.Length);
        return sb.ToString();
    }
}
