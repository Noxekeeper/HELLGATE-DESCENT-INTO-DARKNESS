using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Master settings for the Gold/Economic module.
/// Stored in <c>HellGateJson/Economic/Economy.json</c>. Mirrors the load/cache shape
/// of <see cref="NoREroMod.Systems.CombatAi.Factions.EnemyFactionsConfig"/>.
///
/// <para>
/// All serializable types live at the namespace level (not nested inside this static class)
/// because Unity's <see cref="JsonUtility"/> reliably deserializes top-level
/// <c>[Serializable]</c> classes but has known issues with single-instance nested-class fields
/// declared inside a <c>static</c> outer class. Arrays of nested classes still work, but a
/// scalar field like <c>Hud</c> would silently fall back to defaults.
/// </para>
/// </summary>
internal static class EconomicConfig
{
    private const float ReloadIntervalSec = 2f;
    private static EconomicSettings _cached;
    private static float _lastLoadTime = -999f;

    public static bool Enable => Get().Enable;
    public static bool DebugLogging => Get().DebugLogging;
    public static int AnimFps => Mathf.Max(1, Get().AnimFps);

    public static bool PhysicsEnabled => Get().PhysicsEnabled;
    public static float PhysicsInitialVelocityX => Get().PhysicsInitialVelocityX;
    public static float PhysicsInitialVelocityY => Get().PhysicsInitialVelocityY;
    public static float PhysicsGravity => Get().PhysicsGravity;
    public static float PhysicsBounceScale => Get().PhysicsBounceScale;
    public static float PhysicsBounceTime => Get().PhysicsBounceTime;
    public static float PickupSpriteScale => Mathf.Max(0.05f, Get().PickupSpriteScale);
    public static float PickupTriggerRadius => Mathf.Max(0.1f, Get().PickupTriggerRadius);
    public static float DropSpawnYOffset => Get().DropSpawnYOffset;

    public static EconomicBigDropSplitSettings BigDropSplit =>
        Get().BigDropSplit ?? new EconomicBigDropSplitSettings();

    public static EconomicHudSettings Hud => Get().Hud ?? new EconomicHudSettings();
    public static EconomicPopupSettings Popup => Get().Popup ?? new EconomicPopupSettings();
    public static EconomicAudioSettings Audio => Get().Audio ?? new EconomicAudioSettings();
    public static EconomicHSceneSettings HSceneEarnings => Get().HSceneEarnings ?? new EconomicHSceneSettings();

    public static EconomicCombatGoldLossSettings CombatGoldLoss =>
        Get().CombatGoldLoss ?? new EconomicCombatGoldLossSettings();

    public static EconomicKnockdownGoldLossSettings KnockdownGoldLoss =>
        Get().KnockdownGoldLoss ?? new EconomicKnockdownGoldLossSettings();

    public static string OnPlayerDeath =>
        string.IsNullOrEmpty(Get().OnPlayerDeath) ? "DropPercent" : Get().OnPlayerDeath;
    public static float DeathDropPercent =>
        Mathf.Clamp01(Get().DeathDropPercent <= 0f ? 0.10f : Get().DeathDropPercent);
    public static bool DeathLossReturnable => Get().DeathLossReturnable;
    public static bool DeathLossShowPopup => Get().DeathLossShowPopup;
    public static long DeathLossMinAmount => Math.Max(0L, Get().DeathLossMinAmount);

    public static EconomicDifficultyMultipliers DifficultyMultipliers
        => Get().DifficultyMultipliers ?? EconomicDifficultyMultipliers.Default();

    public static EconomicSettings Get()
    {
        if (_cached != null && Time.realtimeSinceStartup - _lastLoadTime < ReloadIntervalSec)
            return _cached;

        _cached = LoadFromFile();
        _lastLoadTime = Time.realtimeSinceStartup;
        return _cached;
    }

    private static EconomicSettings LoadFromFile()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
            return EconomicSettings.Default();

        try
        {
            string json = File.ReadAllText(path);
            if (!string.IsNullOrEmpty(json) && json[0] == '\uFEFF')
                json = json.TrimStart('\uFEFF');

            // We do NOT use UnityEngine.JsonUtility here. It silently produces empty arrays
            // and default-valued nested objects on PowerShell-formatted JSON, which is the
            // same reason DropSystem ships its own regex fallback. Instead use the small
            // hand-rolled reader in EconomicJsonParser. Robust on the shapes we ship.
            EconomicSettings settings = ParseSettings(json);
            return settings ?? EconomicSettings.Default();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Economic] Failed to load Economy.json: " + ex.Message + ". Using defaults.");
            return EconomicSettings.Default();
        }
    }

    private static EconomicSettings ParseSettings(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        EconomicSettings s = new EconomicSettings();

        s.Enable = EconomicJsonParser.ReadBool(json, "Enable", s.Enable);
        s.DebugLogging = EconomicJsonParser.ReadBool(json, "DebugLogging", s.DebugLogging);
        s.AnimFps = EconomicJsonParser.ReadInt(json, "AnimFps", s.AnimFps);

        s.PhysicsEnabled = EconomicJsonParser.ReadBool(json, "PhysicsEnabled", s.PhysicsEnabled);
        s.PhysicsInitialVelocityX = EconomicJsonParser.ReadFloat(json, "PhysicsInitialVelocityX", s.PhysicsInitialVelocityX);
        s.PhysicsInitialVelocityY = EconomicJsonParser.ReadFloat(json, "PhysicsInitialVelocityY", s.PhysicsInitialVelocityY);
        s.PhysicsGravity = EconomicJsonParser.ReadFloat(json, "PhysicsGravity", s.PhysicsGravity);
        s.PhysicsBounceScale = EconomicJsonParser.ReadFloat(json, "PhysicsBounceScale", s.PhysicsBounceScale);
        s.PhysicsBounceTime = EconomicJsonParser.ReadFloat(json, "PhysicsBounceTime", s.PhysicsBounceTime);
        s.PickupSpriteScale = EconomicJsonParser.ReadFloat(json, "PickupSpriteScale", s.PickupSpriteScale);
        s.PickupTriggerRadius = EconomicJsonParser.ReadFloat(json, "PickupTriggerRadius", s.PickupTriggerRadius);
        s.DropSpawnYOffset = EconomicJsonParser.ReadFloat(json, "DropSpawnYOffset", s.DropSpawnYOffset);

        string splitBody = EconomicJsonParser.ReadObjectBlock(json, "BigDropSplit");
        if (!string.IsNullOrEmpty(splitBody))
        {
            EconomicBigDropSplitSettings b = s.BigDropSplit ?? new EconomicBigDropSplitSettings();
            b.Enable = EconomicJsonParser.ReadBool(splitBody, "Enable", b.Enable);
            b.SmallSplitMinTotalGold = EconomicJsonParser.ReadInt(splitBody, "SmallSplitMinTotalGold", b.SmallSplitMinTotalGold);
            b.SmallSplitPileCount = EconomicJsonParser.ReadInt(splitBody, "SmallSplitPileCount", b.SmallSplitPileCount);
            b.MinTotalGold = EconomicJsonParser.ReadInt(splitBody, "MinTotalGold", b.MinTotalGold);
            b.MinPiles = EconomicJsonParser.ReadInt(splitBody, "MinPiles", b.MinPiles);
            b.MaxPiles = EconomicJsonParser.ReadInt(splitBody, "MaxPiles", b.MaxPiles);
            b.HorizontalSpread = EconomicJsonParser.ReadFloat(splitBody, "HorizontalSpread", b.HorizontalSpread);
            s.BigDropSplit = b;
        }

        s.OnPlayerDeath = EconomicJsonParser.ReadString(json, "OnPlayerDeath", s.OnPlayerDeath);
        s.DeathDropPercent = EconomicJsonParser.ReadFloat(json, "DeathDropPercent", s.DeathDropPercent);
        s.DeathLossReturnable = EconomicJsonParser.ReadBool(json, "DeathLossReturnable", s.DeathLossReturnable);
        s.DeathLossShowPopup = EconomicJsonParser.ReadBool(json, "DeathLossShowPopup", s.DeathLossShowPopup);
        s.DeathLossMinAmount = EconomicJsonParser.ReadInt(json, "DeathLossMinAmount", s.DeathLossMinAmount);

        string combatLossBody = EconomicJsonParser.ReadObjectBlock(json, "CombatGoldLoss");
        if (!string.IsNullOrEmpty(combatLossBody))
        {
            EconomicCombatGoldLossSettings c = s.CombatGoldLoss ?? new EconomicCombatGoldLossSettings();
            c.Enable = EconomicJsonParser.ReadBool(combatLossBody, "Enable", c.Enable);
            c.ChanceOnDamage = EconomicJsonParser.ReadFloat(combatLossBody, "ChanceOnDamage", c.ChanceOnDamage);
            c.MinLossAmount = EconomicJsonParser.ReadInt(combatLossBody, "MinLossAmount", c.MinLossAmount);
            c.MaxLossAmount = EconomicJsonParser.ReadInt(combatLossBody, "MaxLossAmount", c.MaxLossAmount);
            c.MinWalletToDrop = EconomicJsonParser.ReadInt(combatLossBody, "MinWalletToDrop", c.MinWalletToDrop);
            c.CooldownSeconds = EconomicJsonParser.ReadFloat(combatLossBody, "CooldownSeconds", c.CooldownSeconds);
            c.SpawnPickupPile = EconomicJsonParser.ReadBool(combatLossBody, "SpawnPickupPile", c.SpawnPickupPile);
            s.CombatGoldLoss = c;
        }

        string knockdownLossBody = EconomicJsonParser.ReadObjectBlock(json, "KnockdownGoldLoss");
        if (!string.IsNullOrEmpty(knockdownLossBody))
        {
            EconomicKnockdownGoldLossSettings k = s.KnockdownGoldLoss ?? new EconomicKnockdownGoldLossSettings();
            k.Enable = EconomicJsonParser.ReadBool(knockdownLossBody, "Enable", k.Enable);
            k.LossPercent = EconomicJsonParser.ReadFloat(knockdownLossBody, "LossPercent", k.LossPercent);
            k.MinLossAmount = EconomicJsonParser.ReadInt(knockdownLossBody, "MinLossAmount", k.MinLossAmount);
            k.CooldownSeconds = EconomicJsonParser.ReadFloat(knockdownLossBody, "CooldownSeconds", k.CooldownSeconds);
            k.SpawnPickupPile = EconomicJsonParser.ReadBool(knockdownLossBody, "SpawnPickupPile", k.SpawnPickupPile);
            k.ShowPopup = EconomicJsonParser.ReadBool(knockdownLossBody, "ShowPopup", k.ShowPopup);
            s.KnockdownGoldLoss = k;
        }

        // ---- Hud ----
        string hudBody = EconomicJsonParser.ReadObjectBlock(json, "Hud");
        if (!string.IsNullOrEmpty(hudBody))
        {
            EconomicHudSettings h = s.Hud ?? new EconomicHudSettings();
            h.Enable = EconomicJsonParser.ReadBool(hudBody, "Enable", h.Enable);
            h.AnchorX = EconomicJsonParser.ReadFloat(hudBody, "AnchorX", h.AnchorX);
            h.AnchorY = EconomicJsonParser.ReadFloat(hudBody, "AnchorY", h.AnchorY);
            h.AnchoredPositionX = EconomicJsonParser.ReadFloat(hudBody, "AnchoredPositionX", h.AnchoredPositionX);
            h.AnchoredPositionY = EconomicJsonParser.ReadFloat(hudBody, "AnchoredPositionY", h.AnchoredPositionY);
            h.IconSizePx = EconomicJsonParser.ReadInt(hudBody, "IconSizePx", h.IconSizePx);
            h.FontSize = EconomicJsonParser.ReadInt(hudBody, "FontSize", h.FontSize);
            h.TextColorHex = EconomicJsonParser.ReadString(hudBody, "TextColorHex", h.TextColorHex);
            s.Hud = h;
        }

        // ---- Popup ----
        string popupBody = EconomicJsonParser.ReadObjectBlock(json, "Popup");
        if (!string.IsNullOrEmpty(popupBody))
        {
            EconomicPopupSettings p = s.Popup ?? new EconomicPopupSettings();
            p.Enable = EconomicJsonParser.ReadBool(popupBody, "Enable", p.Enable);
            p.RiseDistance = EconomicJsonParser.ReadFloat(popupBody, "RiseDistance", p.RiseDistance);
            p.DurationSec = EconomicJsonParser.ReadFloat(popupBody, "DurationSec", p.DurationSec);
            p.FadeStartFraction = EconomicJsonParser.ReadFloat(popupBody, "FadeStartFraction", p.FadeStartFraction);
            p.FontSize = EconomicJsonParser.ReadInt(popupBody, "FontSize", p.FontSize);
            p.TextColorHex = EconomicJsonParser.ReadString(popupBody, "TextColorHex", p.TextColorHex);
            s.Popup = p;
        }

        // ---- Audio ----
        string audioBody = EconomicJsonParser.ReadObjectBlock(json, "Audio");
        if (!string.IsNullOrEmpty(audioBody))
        {
            EconomicAudioSettings a = s.Audio ?? new EconomicAudioSettings();
            a.Enable = EconomicJsonParser.ReadBool(audioBody, "Enable", a.Enable);
            a.DropFolder = EconomicJsonParser.ReadString(audioBody, "DropFolder", a.DropFolder);
            a.PickupFolder = EconomicJsonParser.ReadString(audioBody, "PickupFolder", a.PickupFolder);
            a.RandomizePickup = EconomicJsonParser.ReadBool(audioBody, "RandomizePickup", a.RandomizePickup);
            a.DropVolume = EconomicJsonParser.ReadFloat(audioBody, "DropVolume", a.DropVolume);
            a.PickupVolume = EconomicJsonParser.ReadFloat(audioBody, "PickupVolume", a.PickupVolume);
            s.Audio = a;
        }

        // ---- DifficultyMultipliers ----
        string diffBody = EconomicJsonParser.ReadObjectBlock(json, "DifficultyMultipliers");
        if (!string.IsNullOrEmpty(diffBody))
        {
            EconomicDifficultyMultipliers d = s.DifficultyMultipliers ?? new EconomicDifficultyMultipliers();
            d.D0 = EconomicJsonParser.ReadFloat(diffBody, "D0", d.D0);
            d.D1 = EconomicJsonParser.ReadFloat(diffBody, "D1", d.D1);
            d.D2 = EconomicJsonParser.ReadFloat(diffBody, "D2", d.D2);
            d.D3 = EconomicJsonParser.ReadFloat(diffBody, "D3", d.D3);
            s.DifficultyMultipliers = d;
        }

        // ---- HSceneEarnings ----
        string hsBody = EconomicJsonParser.ReadObjectBlock(json, "HSceneEarnings");
        if (!string.IsNullOrEmpty(hsBody))
        {
            EconomicHSceneSettings hs = s.HSceneEarnings ?? new EconomicHSceneSettings();
            hs.Enable = EconomicJsonParser.ReadBool(hsBody, "Enable", hs.Enable);
            var rows = EconomicJsonParser.ReadObjectArray(hsBody, "PerFaction");
            if (rows != null && rows.Count > 0)
            {
                EconomicHSceneFactionRule[] arr = new EconomicHSceneFactionRule[rows.Count];
                for (int i = 0; i < rows.Count; i++)
                {
                    arr[i] = new EconomicHSceneFactionRule
                    {
                        Faction = EconomicJsonParser.ReadString(rows[i], "Faction", null),
                        MinAmount = EconomicJsonParser.ReadInt(rows[i], "MinAmount", 0),
                        MaxAmount = EconomicJsonParser.ReadInt(rows[i], "MaxAmount", 0)
                    };
                }
                hs.PerFaction = arr;
            }
            s.HSceneEarnings = hs;
        }

        return s;
    }

    public static string GetConfigPath()
    {
        string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Economic");
        return Path.Combine(dir, "Economy.json");
    }

    public static void Initialize()
    {
        EconomicSettings s = Get();
        if (s == null) return;
        if (s.DebugLogging)
        {
            // Echo a few fields so we can verify nested objects deserialized correctly.
            EconomicHudSettings hud = s.Hud ?? new EconomicHudSettings();
            Plugin.Log?.LogInfo(
                $"[Economic] Config loaded. Enable={s.Enable} OnDeath={s.OnPlayerDeath} " +
                $"HudAnchor=({hud.AnchorX},{hud.AnchorY}) HudPos=({hud.AnchoredPositionX},{hud.AnchoredPositionY})");
        }
    }
}

[Serializable]
public class EconomicSettings
{
    public bool Enable = true;
    public bool DebugLogging = false;

    public int AnimFps = 9;

    public bool PhysicsEnabled = true;
    public float PhysicsInitialVelocityX = 3.5f;
    public float PhysicsInitialVelocityY = 5.0f;
    public float PhysicsGravity = 9.81f;
    public float PhysicsBounceScale = 1.15f;
    public float PhysicsBounceTime = 0.1f;
    public float PickupSpriteScale = 1.25f;
    public float PickupTriggerRadius = 0.55f;
    /// <summary>
    /// Y-offset from <c>enemy.transform.position</c> to the spawn point. Most NoR enemy
    /// transforms have their pivot at chest height, so vanilla loot uses <c>y - 1f</c>
    /// (see <c>TouzokuNormal</c> drop spawn) to put the pickup on the floor. The coroutine
    /// physics then arcs the pile up and back down to this same Y.
    /// </summary>
    public float DropSpawnYOffset = -1.0f;

    /// <summary>
    /// Tiered multi-pile drops: medium totals use <see cref="EconomicBigDropSplitSettings.SmallSplitPileCount"/>;
    /// large totals use random <see cref="EconomicBigDropSplitSettings.MinPiles"/>–<see cref="EconomicBigDropSplitSettings.MaxPiles"/>.
    /// </summary>
    public EconomicBigDropSplitSettings BigDropSplit = new EconomicBigDropSplitSettings();

    public EconomicHudSettings Hud = new EconomicHudSettings();
    public EconomicPopupSettings Popup = new EconomicPopupSettings();
    public EconomicAudioSettings Audio = new EconomicAudioSettings();

    public string OnPlayerDeath = "DropPercent"; // "Keep" | "DropAll" | "DropPercent"
    /// <summary>Fraction of wallet lost on death when mode is DropPercent (0.10 = 10%).</summary>
    public float DeathDropPercent = 0.10f;
    /// <summary>When false, gold is destroyed (no souls-style pile). Default for DropPercent.</summary>
    public bool DeathLossReturnable = false;
    /// <summary>Floating −N popup on permanent death loss.</summary>
    public bool DeathLossShowPopup = true;
    /// <summary>Minimum gold removed on death when percent rounds to zero (0 = disabled).</summary>
    public int DeathLossMinAmount = 0;

    public EconomicCombatGoldLossSettings CombatGoldLoss = new EconomicCombatGoldLossSettings();

    public EconomicKnockdownGoldLossSettings KnockdownGoldLoss = new EconomicKnockdownGoldLossSettings();

    public EconomicDifficultyMultipliers DifficultyMultipliers = new EconomicDifficultyMultipliers();

    public EconomicHSceneSettings HSceneEarnings = new EconomicHSceneSettings();

    public static EconomicSettings Default() => new EconomicSettings();
}

[Serializable]
public class EconomicBigDropSplitSettings
{
    public bool Enable = true;
    /// <summary>At or above this total (and below <see cref="MinTotalGold"/>), spawn <see cref="SmallSplitPileCount"/> piles.</summary>
    public int SmallSplitMinTotalGold = 15;
    /// <summary>Number of pickups for the medium tier (e.g. 2). Set below 2 to disable this tier.</summary>
    public int SmallSplitPileCount = 2;
    /// <summary>Enemy death drop totals at or above this value spawn the large random pile count (after multipliers).</summary>
    public int MinTotalGold = 80;
    public int MinPiles = 8;
    public int MaxPiles = 11;
    /// <summary>Random X offset in world units for each pile (±this range).</summary>
    public float HorizontalSpread = 0.45f;
}

[Serializable]
public class EconomicHudSettings
{
    public bool Enable = true;
    public float AnchorX = 0f;
    public float AnchorY = 0f;
    public float AnchoredPositionX = 20f;
    public float AnchoredPositionY = 20f;
    public int IconSizePx = 32;
    public int FontSize = 28;
    public string TextColorHex = "#FFC83D";
}

[Serializable]
public class EconomicPopupSettings
{
    public bool Enable = true;
    public float RiseDistance = 0.6f;
    public float DurationSec = 0.9f;
    public float FadeStartFraction = 0.55f;
    public int FontSize = 28;
    public string TextColorHex = "#FFD24A";
}

[Serializable]
public class EconomicAudioSettings
{
    public bool Enable = true;
    public string DropFolder = "EconomicHG/Audio/NormalDrop";
    public string PickupFolder = "EconomicHG/Audio/PickUpGold";
    public bool RandomizePickup = false;
    public float DropVolume = 0.85f;
    public float PickupVolume = 1.0f;
}

[Serializable]
public class EconomicHSceneSettings
{
    public bool Enable = true;
    public EconomicHSceneFactionRule[] PerFaction = new EconomicHSceneFactionRule[0];
}

[Serializable]
public class EconomicHSceneFactionRule
{
    public string Faction;
    public int MinAmount;
    public int MaxAmount;
}

[Serializable]
public class EconomicCombatGoldLossSettings
{
    public bool Enable = false;
    /// <summary>Per qualifying player hit (after cooldown). Includes guard blocks.</summary>
    public float ChanceOnDamage = 0.03f;
    public int MinLossAmount = 1;
    public int MaxLossAmount = 3;
    public int MinWalletToDrop = 1;
    public float CooldownSeconds = 0.35f;
    public bool SpawnPickupPile = true;
}

[Serializable]
public class EconomicKnockdownGoldLossSettings
{
    public bool Enable = true;
    /// <summary>Fraction of wallet lost on combat knockdown (0.01 = 1%). Permanent by default.</summary>
    public float LossPercent = 0.01f;
    public int MinLossAmount = 0;
    public float CooldownSeconds = 0.5f;
    public bool SpawnPickupPile = false;
    public bool ShowPopup = true;
}

[Serializable]
public class EconomicDifficultyMultipliers
{
    public float D0 = 0.8f;
    public float D1 = 1.0f;
    public float D2 = 1.2f;
    public float D3 = 1.5f;

    public float Resolve(int difficulty)
    {
        switch (difficulty)
        {
            case 0: return Mathf.Max(0.01f, D0);
            case 2: return Mathf.Max(0.01f, D2);
            case 3: return Mathf.Max(0.01f, D3);
            default: return Mathf.Max(0.01f, D1);
        }
    }

    public static EconomicDifficultyMultipliers Default() => new EconomicDifficultyMultipliers();
}
