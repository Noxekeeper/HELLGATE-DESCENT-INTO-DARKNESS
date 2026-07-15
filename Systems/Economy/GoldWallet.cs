using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Player gold wallet. Per-save-slot persistence mirrors
/// <see cref="NoREroMod.Systems.CombatAi.Factions.PlayerFactionReputation"/>:
/// state lives in memory, is bound to slot 1..3 on the next manual Save/Load, and is
/// serialized to <c>BepInEx/plugins/HellGateJson/Economic/PlayerGold_Slot{NN}.json</c>.
/// Souls-style "lost pile" snapshot is stored in the same file together with the balance.
/// </summary>
internal static class GoldWallet
{
    private const string SaveFileNameFormat = "PlayerGold_Slot{0:00}.json";

    private static long _current;
    private static int _activeSlotZeroBased = -1;
    private static bool _dirty;

    /// <summary>Fires after the balance changes. Args: (oldValue, newValue).</summary>
    public static event Action<long, long> OnChanged;

    public static long Current => _current;
    public static int ActiveSlotOneBased => _activeSlotZeroBased < 0 ? 0 : _activeSlotZeroBased + 1;

    public static void Initialize()
    {
        // No I/O here — slot is bound only on first manual Save/Load (matches reputation behavior).
        // Reset transient state so a stale value from a previous session does not bleed.
        _current = 0;
        GoldStaticMng.Clear();
        _activeSlotZeroBased = -1;
        _dirty = false;
        if (EconomicConfig.DebugLogging)
            Plugin.Log?.LogInfo("[GoldWallet] Initialized (volatile until first save/load).");
    }

    /// <summary>Reload balance from disk if a slot is already bound (after Save/Load). Otherwise balance is RAM-only.</summary>
    public static void ReloadBoundSlotIfAny()
    {
        if (_activeSlotZeroBased < 0)
            return;
        LoadFromActiveSlot();
    }

    /// <summary>
    /// Write balance to JSON immediately (only if a slot is already bound via Load/Save).
    /// Do not call from gameplay (dialogs, drops) — otherwise gold lives apart from the game save point.
    /// Persistence only via <see cref="SaveToActiveSlot"/> on the SaveFile hook.
    /// </summary>
    [Obsolete("Gameplay must not flush; use Save hook only.")]
    public static void FlushToDiskIfBound() => SaveToActiveSlot(force: true);

    public static void ModifyGold(long delta)
    {
        if (delta == 0) return;
        SetGold(_current + delta);
    }

    public static void SetGold(long value)
    {
        long clamped = Math.Max(0, value);
        if (_current == clamped) return;
        long old = _current;
        _current = clamped;
        _dirty = true;
        try { OnChanged?.Invoke(old, _current); }
        catch (Exception ex) { Plugin.Log?.LogWarning("[GoldWallet] OnChanged subscriber threw: " + ex.Message); }
    }

    public static void ResetAll()
    {
        long old = _current;
        _current = 0;
        GoldStaticMng.Clear();
        _dirty = true;
        if (old != 0)
        {
            try { OnChanged?.Invoke(old, 0); } catch { }
        }
    }

    // ---- slot binding ----

    public static void BindActiveSlot(int slotZeroBased)
    {
        if (slotZeroBased < 0 || slotZeroBased > 2)
        {
            Plugin.Log?.LogWarning("[GoldWallet] BindActiveSlot rejected: slot=" + slotZeroBased + " (expected 0..2)");
            return;
        }
        _activeSlotZeroBased = slotZeroBased;
        if (EconomicConfig.DebugLogging)
            Plugin.Log?.LogInfo("[GoldWallet] Active slot bound to " + (slotZeroBased + 1));
    }

    public static void LoadFromActiveSlot()
    {
        if (_activeSlotZeroBased < 0)
        {
            Plugin.Log?.LogWarning("[GoldWallet] LoadFromActiveSlot called with no active slot bound.");
            return;
        }

        string path = GetSavePathForSlot(_activeSlotZeroBased);
        bool exists = File.Exists(path);
        if (EconomicConfig.DebugLogging)
            Plugin.Log?.LogInfo("[GoldWallet] Loading slot " + ActiveSlotOneBased + " from '" + path + "' (exists=" + exists + ")");

        long old = _current;
        _current = 0;
        GoldStaticMng.Clear();
        _dirty = false;

        if (!exists)
        {
            try { OnChanged?.Invoke(old, _current); } catch { }
            return;
        }

        try
        {
            string raw = File.ReadAllText(path);
            ApplyJson(raw);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldWallet] Load failed for slot " + ActiveSlotOneBased + ": " + ex.Message);
        }

        try { OnChanged?.Invoke(old, _current); } catch { }
    }

    // Hand-rolled regex-based parser — UnityEngine.JsonUtility cannot deserialize into a static
    // class (and our wallet state spans GoldWallet + GoldStaticMng anyway). Mirrors the regex
    // strategy used by PlayerFactionReputation.
    private static readonly Regex GoldPattern = new Regex(
        "\"Gold\"\\s*:\\s*(\\d+)",
        RegexOptions.CultureInvariant);
    private static readonly Regex LostBlockPattern = new Regex(
        "\"Lost\"\\s*:\\s*\\{(?<body>[^}]*)\\}",
        RegexOptions.CultureInvariant);
    private static readonly Regex LostActivePattern = new Regex("\"Active\"\\s*:\\s*(true|false)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex LostAmountPattern = new Regex("\"Amount\"\\s*:\\s*(\\d+)", RegexOptions.CultureInvariant);
    private static readonly Regex LostScenePattern = new Regex("\"Scene\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant);
    private static readonly Regex LostXPattern = new Regex("\"X\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)", RegexOptions.CultureInvariant);
    private static readonly Regex LostYPattern = new Regex("\"Y\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)", RegexOptions.CultureInvariant);

    private static void ApplyJson(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return;

        Match goldMatch = GoldPattern.Match(raw);
        if (goldMatch.Success && long.TryParse(goldMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long gold))
        {
            _current = Math.Max(0, gold);
        }

        Match block = LostBlockPattern.Match(raw);
        if (!block.Success)
            return;
        string body = block.Groups["body"].Value;

        bool active = false;
        Match m = LostActivePattern.Match(body);
        if (m.Success) active = string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        if (!active) return;

        long lostAmount = 0;
        m = LostAmountPattern.Match(body);
        if (m.Success) long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out lostAmount);

        string scene = string.Empty;
        m = LostScenePattern.Match(body);
        if (m.Success) scene = m.Groups[1].Value;

        float lx = 0f, ly = 0f;
        m = LostXPattern.Match(body);
        if (m.Success) float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lx);
        m = LostYPattern.Match(body);
        if (m.Success) float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out ly);

        if (lostAmount > 0 && !string.IsNullOrEmpty(scene))
        {
            GoldStaticMng.Set(lostAmount, scene, new Vector2(lx, ly));
        }
    }

    public static void SaveToActiveSlot(bool force)
    {
        if (_activeSlotZeroBased < 0)
        {
            if (force)
                Plugin.Log?.LogWarning("[GoldWallet] SaveToActiveSlot(force=true) skipped — no active slot bound");
            return;
        }
        if (!force && !_dirty) return;

        try
        {
            string path = GetSavePathForSlot(_activeSlotZeroBased);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"Gold\": ").Append(_current.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\n  \"Lost\": {\n");
            sb.Append("    \"Active\": ").Append(GoldStaticMng.LostFlag ? "true" : "false").Append(",\n");
            sb.Append("    \"Amount\": ").Append(GoldStaticMng.LostAmount.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("    \"Scene\": \"").Append(EscapeJsonString(GoldStaticMng.LostScene ?? string.Empty)).Append("\",\n");
            sb.Append("    \"X\": ").Append(GoldStaticMng.LostPos.x.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("    \"Y\": ").Append(GoldStaticMng.LostPos.y.ToString("0.###", CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("  }\n}\n");

            File.WriteAllText(path, sb.ToString());
            _dirty = false;
            if (EconomicConfig.DebugLogging)
                Plugin.Log?.LogInfo($"[GoldWallet] Saved slot {ActiveSlotOneBased}: gold={_current} lost={GoldStaticMng.LostFlag}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldWallet] Save failed for slot " + ActiveSlotOneBased + ": " + ex.Message);
        }
    }

    private static string EscapeJsonString(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GetSavePathForSlot(int slotZeroBased)
    {
        string fileName = string.Format(SaveFileNameFormat, slotZeroBased + 1);
        string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Economic");
        return Path.Combine(dir, fileName);
    }
}
