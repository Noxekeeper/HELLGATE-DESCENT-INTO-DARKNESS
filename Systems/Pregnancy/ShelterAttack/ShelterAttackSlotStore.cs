using System;
using System.IO;
using System.Text;
using BepInEx;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

internal static class ShelterAttackSlotStore
{
    private static int _activeSlot = -1;
    private static bool _dirty;

    private static string GetFilePath(int slot)
    {
        string fileName = string.Format("ShelterAttack_Slot{0:D2}.json", slot);
        return ResolveFilePath(fileName);
    }

    private static string ResolveFilePath(string fileName)
    {
        try
        {
            string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Pregnancy");
            try { Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, fileName);
        }
        catch
        {
            string gameRoot = Application.dataPath;
            if (gameRoot.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
                gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

            string dir = Path.Combine(Path.Combine(Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"), "HellGateJson"), "Pregnancy");
            try { Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, fileName);
        }
    }

    public static void BindActiveSlot(int slot)
    {
        if (slot < 1 || slot > 3)
            return;
        _activeSlot = slot;
    }

    /// <summary>Marks in-memory shelter-attack state changed; disk write only on vanilla Save hook.</summary>
    public static void MarkDirty()
    {
        _dirty = true;
    }

    public static void SaveToActiveSlot(bool force = false)
    {
        if (_activeSlot < 1)
            return;
        if (!force && !_dirty)
            return;
        Save(_activeSlot);
    }

    public static void LoadFromActiveSlot()
    {
        if (_activeSlot < 1)
            return;
        Load(_activeSlot);
    }

    private static void Save(int slot)
    {
        string path = GetFilePath(slot);
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"version\": 1,");
            sb.AppendLine($"  \"slot\": {slot},");
            sb.AppendLine($"  \"savedAt\": \"{DateTime.UtcNow:O}\",");
            sb.AppendLine($"  \"phase\": {(int)ShelterAttackState.Phase},");
            sb.AppendLine($"  \"attackingFaction\": {ShelterAttackState.AttackingFaction},");
            sb.AppendLine($"  \"utcDeadlineSeconds\": {ShelterAttackState.UtcDeadlineSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"currentWave\": {ShelterAttackState.CurrentWave},");
            sb.AppendLine($"  \"spawnIndexInWave\": {ShelterAttackState.SpawnIndexInWave},");
            float waveBreakRemaining = ShelterAttackState.Phase == ShelterAttackPhase.WaveBreak
                ? ShelterAttackState.GetWaveBreakRemainingSeconds()
                : 0f;
            sb.AppendLine($"  \"waveBreakRemainingSeconds\": {waveBreakRemaining.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"threatTier\": {(int)ShelterAttackState.ThreatTier},");
            sb.AppendLine($"  \"threatTierLocked\": {ShelterAttackState.ThreatTierLocked.ToString().ToLowerInvariant()}");
            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            _dirty = false;

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.ShelterAttack.Store] Saved slot {slot}: phase={ShelterAttackState.Phase}, faction={ShelterAttackState.AttackingFaction}, deadline={ShelterAttackState.UtcDeadlineSeconds}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.ShelterAttack.Store] Failed to save slot {slot}: {ex.Message}");
        }
    }

    private static void Load(int slot)
    {
        string path = GetFilePath(slot);
        _dirty = false;

        if (!File.Exists(path))
        {
            ShelterAttackState.Reset();
            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.ShelterAttack.Store] No save file for slot {slot}, starting fresh");
            return;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            ParseJson(json);

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.ShelterAttack.Store] Loaded slot {slot}: phase={ShelterAttackState.Phase}, faction={ShelterAttackState.AttackingFaction}, deadline={ShelterAttackState.UtcDeadlineSeconds}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.ShelterAttack.Store] Failed to load slot {slot}: {ex.Message}");
            ShelterAttackState.Reset();
        }
    }

    private static void ParseJson(string json)
    {
        ShelterAttackState.Reset();

        var lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int phase = 0;
        int faction = FactionIds.Neutral;
        double deadline = 0;
        int wave = 0;
        int spawnIndex = 0;
        float waveBreakRemaining = 0f;
        int threatTier = 0;
        bool threatTierLocked = false;
        bool hasThreatTier = false;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("\""))
                continue;

            int colonIdx = line.IndexOf(':');
            if (colonIdx < 0)
                continue;

            string key = line.Substring(1, line.IndexOf('"', 1) - 1);
            string value = line.Substring(colonIdx + 1).Trim().TrimEnd(',', '"');

            switch (key)
            {
                case "phase": int.TryParse(value, out phase); break;
                case "attackingFaction": int.TryParse(value, out faction); break;
                case "utcDeadlineSeconds": double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out deadline); break;
                case "currentWave": int.TryParse(value, out wave); break;
                case "spawnIndexInWave": int.TryParse(value, out spawnIndex); break;
                case "waveBreakRemainingSeconds":
                    float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out waveBreakRemaining);
                    break;
                case "threatTier":
                    if (int.TryParse(value, out threatTier))
                        hasThreatTier = true;
                    break;
                case "threatTierLocked":
                    threatTierLocked = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        if (phase >= 0 && phase <= 7)
        {
            ShelterAttackState.Phase = (ShelterAttackPhase)phase;
            ShelterAttackState.AttackingFaction = faction;
            ShelterAttackState.UtcDeadlineSeconds = deadline;
            ShelterAttackState.CurrentWave = wave;
            ShelterAttackState.SpawnIndexInWave = Mathf.Max(0, spawnIndex);
            ShelterAttackState.WaveBreakUntilUnscaled = Time.unscaledTime + Mathf.Max(0f, waveBreakRemaining);

            if (hasThreatTier)
            {
                ShelterAttackState.ThreatTier = (ShelterAttackWaves.ThreatTier)Mathf.Clamp(threatTier, 0, 2);
                ShelterAttackState.ThreatTierLocked = threatTierLocked || ShelterAttackState.IsEventActive;
            }
            else if (ShelterAttackState.IsEventActive)
            {
                // Legacy slot: infer tier from current hideout children and lock it.
                int children = PregnancySlotStore.GetAliveChildrenInHideout().Count;
                ShelterAttackState.ThreatTier = ShelterAttackWaves.ResolveThreatTier(children);
                ShelterAttackState.ThreatTierLocked = true;
            }
        }
    }
}
