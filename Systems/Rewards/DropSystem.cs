using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using GameDataEditor;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Rewards;

internal enum DropRewardType
{
    None,
    Item,
    Use,
    Ring,
    Costume,
    Magic
}

/// <summary>Must stay <c>public</c> for Unity <see cref="JsonUtility"/>; regex fallback parses <c>drops</c> when JsonUtility fails.</summary>
[Serializable]
public sealed class DropTableSettings
{
    public int rollCount = 1;
    public bool autoPickup = false;
    public int noDropWeight = 0;
}

[Serializable]
public sealed class DropTableEntry
{
    public string type = "none";
    public string key = string.Empty;
    public int id = -1;
    public int weight = 0;
}

[Serializable]
public sealed class DropTableConfig
{
    public DropTableSettings settings = new DropTableSettings();
    public DropTableEntry[] drops = new DropTableEntry[0];
}

internal struct DropRollResult
{
    public DropRewardType RewardType;
    public int RewardId;
    public bool IsValid;
}

/// <summary>
/// Generic weighted drop system for enemy rewards.
/// The module is data-driven and can be reused by any enemy type.
/// </summary>
internal static class DropSystem
{
    private sealed class ResolvedDropEntry
    {
        internal DropTableEntry Entry;
        internal DropRewardType RewardType;
        internal int ResolvedId;
        /// <summary>Sum of JSON weights for this resolved reward (merged duplicate rows).</summary>
        internal int Weight;
    }

    /// <summary>Caches <see cref="GDEcrestringData"/> / use / magic row resolution by key.</summary>
    private static readonly Dictionary<string, int> s_ringIdByKeyRuntime = new Dictionary<string, int>(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> s_useIdByKeyRuntime = new Dictionary<string, int>(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> s_magicIdByKeyRuntime = new Dictionary<string, int>(StringComparer.Ordinal);

    internal static bool TryLoadConfig(string jsonPath, out DropTableConfig config)
    {
        config = null;
        if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            return false;

        try
        {
            string json = File.ReadAllText(jsonPath);
            // PowerShell often writes UTF-8 with BOM; Unity's JsonUtility rejects leading U+FEFF.
            if (!string.IsNullOrEmpty(json) && json[0] == '\uFEFF')
                json = json.TrimStart('\uFEFF');

            DropTableConfig fromJu = null;
            try
            {
                fromJu = JsonUtility.FromJson<DropTableConfig>(json);
            }
            catch
            {
                fromJu = null;
            }

            DropTableConfig fromRx = TryParseDropTableRegex(json);

            int nJu = fromJu != null && fromJu.drops != null ? fromJu.drops.Length : 0;
            int nRx = fromRx != null && fromRx.drops != null ? fromRx.drops.Length : 0;

            if (nRx > nJu)
            {
                config = fromRx;
                if (nJu == 0)
                    Plugin.Log?.LogInfo($"[drop-system] Regex parsed {nRx} drop rows (JsonUtility had 0). Path: {jsonPath}");
            }
            else if (nJu > 0)
                config = fromJu;
            else
                config = fromRx ?? fromJu;

            if (config == null)
                return false;
            if (config.settings == null)
                config.settings = new DropTableSettings();
            if (config.drops == null)
                config.drops = new DropTableEntry[0];
            if (config.drops.Length == 0 && json.IndexOf("\"drops\"", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Plugin.Log?.LogWarning(
                    "[drop-system] JSON references \"drops\" but 0 rows parsed after Regex + JsonUtility. Biscord will use multi-ring emergency table.");
            }
            Plugin.Log?.LogInfo($"[drop-system] Loaded drop table: {config.drops.Length} entries from {jsonPath}");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[drop-system] Failed to parse drop config: {jsonPath}. {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// HellGate JSON uses PowerShell-expanded spacing; <see cref="JsonUtility"/> often leaves <c>drops</c> empty.
    /// This scans for <c>type/key/id/weight</c> object fields in order (matches shipped <c>biscord-drop-table.json</c>).
    /// </summary>
    private static DropTableConfig TryParseDropTableRegex(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            var cfg = new DropTableConfig();
            cfg.settings = new DropTableSettings
            {
                rollCount = TryParseJsonIntAfterKey(json, "rollCount", 1),
                autoPickup = TryParseJsonBoolAfterKey(json, "autoPickup", false),
                noDropWeight = TryParseJsonIntAfterKey(json, "noDropWeight", 0)
            };

            // Order in file: "type", "key", "id", "weight" (allows extra whitespace / newlines).
            const string pat =
                "\"type\"\\s*:\\s*\"(?<type>[^\"]+)\"\\s*,\\s*" +
                "\"key\"\\s*:\\s*\"(?<key>[^\"]*)\"\\s*,\\s*" +
                "\"id\"\\s*:\\s*(?<id>-?\\d+)\\s*,\\s*" +
                "\"weight\"\\s*:\\s*(?<weight>\\d+)";

            List<DropTableEntry> list = new List<DropTableEntry>();
            for (Match m = Regex.Match(json, pat, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                 m.Success;
                 m = m.NextMatch())
            {
                int id = int.Parse(m.Groups["id"].Value, System.Globalization.CultureInfo.InvariantCulture);
                int weight = int.Parse(m.Groups["weight"].Value, System.Globalization.CultureInfo.InvariantCulture);
                list.Add(new DropTableEntry
                {
                    type = m.Groups["type"].Value,
                    key = m.Groups["key"].Value,
                    id = id,
                    weight = weight
                });
            }

            if (list.Count == 0)
                return null;

            cfg.drops = list.ToArray();
            return cfg;
        }
        catch
        {
            return null;
        }
    }

    private static int TryParseJsonIntAfterKey(string json, string key, int defaultValue)
    {
        try
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!m.Success)
                return defaultValue;
            return int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return defaultValue;
        }
    }

    private static bool TryParseJsonBoolAfterKey(string json, string key, bool defaultValue)
    {
        try
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!m.Success)
                return defaultValue;
            return string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>Fallback table: one weighted row per real GDE ring key (not <c>non_frame</c>), so emergency is not stuck on Worn Crest / ring_old only.</summary>
    internal static DropTableConfig CreateBiscordEmergencyDropTable()
    {
        List<DropTableEntry> drops = new List<DropTableEntry>();
        try
        {
            List<GDEcrestringData> rings = GDEDataManager.GetAllItems<GDEcrestringData>();
            for (int i = 0; i < rings.Count; i++)
            {
                GDEcrestringData x = rings[i];
                if (x == null || string.IsNullOrEmpty(x.Key))
                    continue;
                string k = x.Key.Trim();
                if (string.Equals(k, "non_frame", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!GdeMasterRowExists(k))
                    continue;
                drops.Add(new DropTableEntry { type = "ring", key = k, id = -1, weight = 1 });
            }
        }
        catch { }

        if (drops.Count == 0)
        {
            int ringId = TryResolveRingIdByGdeRow("ring_old");
            if (ringId < 0)
                ringId = 1;
            drops.Add(new DropTableEntry { type = "ring", key = string.Empty, id = ringId, weight = 1 });
        }

        return new DropTableConfig
        {
            settings = new DropTableSettings { rollCount = 1, autoPickup = false, noDropWeight = 0 },
            drops = drops.ToArray()
        };
    }

    internal static bool HasAnyWeightedTypedDrop(DropTableConfig config)
    {
        if (config?.drops == null)
            return false;
        for (int i = 0; i < config.drops.Length; i++)
        {
            DropTableEntry e = config.drops[i];
            if (e == null || e.weight <= 0)
                continue;
            if (TryParseRewardType(e.type, out DropRewardType rt) && rt != DropRewardType.None)
                return true;
        }
        return false;
    }

    /// <summary>Stable key for a resolved drop (multi-roll de-dupe).</summary>
    internal static string ResolvedRewardKey(DropRewardType type, int id) => ((int)type).ToString() + ":" + id.ToString();

    /// <param name="excludeResolved">Optional: keys from <see cref="ResolvedRewardKey"/> already awarded this death.</param>
    internal static DropRollResult Roll(DropTableConfig config, HashSet<string> excludeResolved = null)
    {
        DropRollResult invalid = new DropRollResult { IsValid = false, RewardType = DropRewardType.None, RewardId = -1 };
        if (config == null || config.drops == null || config.drops.Length == 0)
            return invalid;

        int noDropWeight = Mathf.Max(0, config.settings != null ? config.settings.noDropWeight : 0);
        List<DropTableEntry> pool = new List<DropTableEntry>(config.drops.Length);
        for (int i = 0; i < config.drops.Length; i++)
        {
            DropTableEntry entry = config.drops[i];
            if (entry == null || entry.weight <= 0)
                continue;
            if (!TryParseRewardType(entry.type, out DropRewardType rt))
                continue;
            if (rt == DropRewardType.None)
                continue;
            pool.Add(entry);
        }

        List<ResolvedDropEntry> rawViable = new List<ResolvedDropEntry>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            DropTableEntry entry = pool[i];
            if (!TryParseRewardType(entry.type, out DropRewardType rewardType))
                continue;
            int resolvedId = ResolveRewardId(rewardType, entry);
            if (resolvedId < 0)
                continue;
            if (excludeResolved != null && excludeResolved.Count > 0 &&
                excludeResolved.Contains(ResolvedRewardKey(rewardType, resolvedId)))
                continue;
            rawViable.Add(new ResolvedDropEntry { Entry = entry, RewardType = rewardType, ResolvedId = resolvedId, Weight = entry.weight });
        }

        List<ResolvedDropEntry> viable = MergeViableDropsByResolvedId(rawViable);

        int totalWeight = noDropWeight;
        for (int i = 0; i < viable.Count; i++)
            totalWeight += viable[i].Weight;

        if (totalWeight <= 0)
        {
            if (noDropWeight > 0)
                return new DropRollResult { IsValid = true, RewardType = DropRewardType.None, RewardId = -1 };

            if (excludeResolved != null && excludeResolved.Count > 0 && pool.Count > 0)
            {
                Plugin.Log?.LogWarning("[drop-system] All weighted entries excluded by prior rolls; no drop this step.");
                return invalid;
            }

            Plugin.Log?.LogWarning("[drop-system] Drop table has no weighted entries (empty or invalid types / unresolved ids).");
            return invalid;
        }

        int roll = UnityEngine.Random.Range(1, totalWeight + 1);
        if (roll <= noDropWeight)
            return new DropRollResult { IsValid = true, RewardType = DropRewardType.None, RewardId = -1 };

        int cursor = noDropWeight;
        for (int i = 0; i < viable.Count; i++)
        {
            ResolvedDropEntry row = viable[i];
            cursor += row.Weight;
            if (roll > cursor)
                continue;

            return new DropRollResult
            {
                IsValid = true,
                RewardType = row.RewardType,
                RewardId = row.ResolvedId
            };
        }

        Plugin.Log?.LogWarning("[drop-system] Roll walk failed (internal weight mismatch).");
        return invalid;
    }

    /// <summary>Many JSON rows can resolve to the same GDE id; merge weights so RNG varies between distinct rewards.</summary>
    private static List<ResolvedDropEntry> MergeViableDropsByResolvedId(List<ResolvedDropEntry> raw)
    {
        if (raw == null || raw.Count == 0)
            return raw ?? new List<ResolvedDropEntry>();

        Dictionary<string, ResolvedDropEntry> map = new Dictionary<string, ResolvedDropEntry>(StringComparer.Ordinal);
        for (int i = 0; i < raw.Count; i++)
        {
            ResolvedDropEntry r = raw[i];
            if (r == null)
                continue;
            string k = ResolvedRewardKey(r.RewardType, r.ResolvedId);
            if (!map.TryGetValue(k, out ResolvedDropEntry acc))
            {
                map[k] = new ResolvedDropEntry
                {
                    Entry = r.Entry,
                    RewardType = r.RewardType,
                    ResolvedId = r.ResolvedId,
                    Weight = Mathf.Max(0, r.Weight)
                };
            }
            else
                acc.Weight += Mathf.Max(0, r.Weight);
        }

        return new List<ResolvedDropEntry>(map.Values);
    }

    private static string NormalizeDropKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;
        return key.Trim();
    }

    /// <summary>
    /// Returns <see cref="EnemyDate"/>'s serialized <c>Drop</c> prefab — the same reference vanilla enemies use for
    /// <c>Instantiate(Drop, position, localRotation).GetComponent&lt;Pickup_Dropitem&gt;()</c>.
    /// </summary>
    internal static GameObject ResolveEnemyDropPickupPrefab(EnemyDate enemy)
    {
        if (enemy == null)
            return null;

        VanillaEnemyDropWiring.EnsureEnemyDateDropReference(enemy);

        try
        {
            GameObject drop = Traverse.Create(enemy).Field("Drop").GetValue<GameObject>();
            return drop;
        }
        catch
        {
            return null;
        }
    }

    internal static int ApplyVanillaExpDifficultyScaling(int baseExp, int gameDifficultyFlag)
    {
        if (baseExp <= 0)
            return 0;

        switch (gameDifficultyFlag)
        {
            case 0:
                return Mathf.FloorToInt(baseExp * 1.1f);
            case 2:
                return Mathf.FloorToInt(baseExp * 1.1f);
            case 3:
                return Mathf.FloorToInt(baseExp * 1.5f);
            default:
                return baseExp;
        }
    }

    /// <summary>
    /// Spawns loot the same way vanilla enemies do: Instantiate the serialized <c>Drop</c> prefab at the enemy transform,
    /// then <see cref="Pickup_Dropitem.DROPItemKind"/> / <see cref="Auto_PickUp_Dropitem.DROPItemKind"/>.
    /// </summary>
    internal static bool TrySpawnDrop(Transform sourceTransform, GameObject dropPrefab, DropRollResult reward, bool forceAutoPickup = false, float spawnYOffset = 0f)
    {
        if (!reward.IsValid)
            return false;
        if (reward.RewardType == DropRewardType.None)
            return true;
        if (sourceTransform == null || dropPrefab == null)
            return false;

        Vector2 spawnPos = (Vector2)sourceTransform.position + new Vector2(0f, spawnYOffset);
        // Matches goblin / most enemies: localRotation on the enemy root, not world rotation.
        GameObject spawned = UnityEngine.Object.Instantiate(dropPrefab, spawnPos, sourceTransform.localRotation);
        if (spawned == null)
            return false;

        int itemId = -1;
        int useId = -1;
        int ringId = -1;
        int costumeId = -1;
        int magicId = -1;
        switch (reward.RewardType)
        {
            case DropRewardType.Item:
                itemId = reward.RewardId;
                break;
            case DropRewardType.Use:
                useId = reward.RewardId;
                break;
            case DropRewardType.Ring:
                ringId = reward.RewardId;
                break;
            case DropRewardType.Costume:
                costumeId = reward.RewardId;
                break;
            case DropRewardType.Magic:
                magicId = reward.RewardId;
                break;
            default:
                UnityEngine.Object.Destroy(spawned);
                return false;
        }

        Pickup_Dropitem pickup = spawned.GetComponent<Pickup_Dropitem>();
        if (pickup == null)
            pickup = spawned.GetComponentInChildren<Pickup_Dropitem>(true);

        Auto_PickUp_Dropitem autoPickup = spawned.GetComponent<Auto_PickUp_Dropitem>();
        if (autoPickup == null)
            autoPickup = spawned.GetComponentInChildren<Auto_PickUp_Dropitem>(true);

        if (forceAutoPickup)
        {
            if (autoPickup != null)
            {
                autoPickup.DROPItemKind(itemId, useId, ringId, costumeId, magicId);
                return true;
            }
            if (pickup != null)
            {
                pickup.DROPItemKind(itemId, useId, ringId, costumeId, magicId);
                return true;
            }
            UnityEngine.Object.Destroy(spawned);
            Plugin.Log?.LogWarning("[drop-system] Drop prefab has neither Auto_PickUp_Dropitem nor Pickup_Dropitem.");
            return false;
        }

        if (pickup != null)
        {
            pickup.DROPItemKind(itemId, useId, ringId, costumeId, magicId);
            return true;
        }
        if (autoPickup != null)
        {
            autoPickup.DROPItemKind(itemId, useId, ringId, costumeId, magicId);
            Plugin.Log?.LogInfo("[drop-system] Ground drop used Auto_PickUp_Dropitem template (touch to collect).");
            return true;
        }

        UnityEngine.Object.Destroy(spawned);
        Plugin.Log?.LogWarning("[drop-system] Drop prefab has no Pickup_Dropitem or Auto_PickUp_Dropitem.");
        return false;
    }

    private static bool TryParseRewardType(string raw, out DropRewardType rewardType)
    {
        rewardType = DropRewardType.None;
        if (string.IsNullOrEmpty(raw))
            return false;

        string s = raw.Trim().ToLowerInvariant();
        switch (s)
        {
            case "none":
                rewardType = DropRewardType.None;
                return true;
            case "item":
                rewardType = DropRewardType.Item;
                return true;
            case "use":
                rewardType = DropRewardType.Use;
                return true;
            case "ring":
                rewardType = DropRewardType.Ring;
                return true;
            case "costume":
            case "cos":
                rewardType = DropRewardType.Costume;
                return true;
            case "magic":
            case "mg":
                rewardType = DropRewardType.Magic;
                return true;
            default:
                return false;
        }
    }

    private static int ResolveRewardId(DropRewardType rewardType, DropTableEntry entry)
    {
        if (entry == null)
            return -1;
        if (entry.id >= 0)
            return entry.id;

        string keyNorm = NormalizeDropKey(entry.key);
        if (string.IsNullOrEmpty(keyNorm))
            return -1;

        switch (rewardType)
        {
            // Do not use LookupKey here: it bypassed GdeMasterRowExists and could return ids for keys not in master data.
            case DropRewardType.Use:
                return TryResolveUseIdByGdeRow(keyNorm);
            case DropRewardType.Ring:
                return TryResolveRingIdByGdeRow(keyNorm);
            case DropRewardType.Magic:
                return TryResolveMagicIdByGdeRow(keyNorm);
            default:
                return entry.id;
        }
    }

    /// <summary>
    /// GDE <see cref="IGDEData"/> constructors still run <see cref="IGDEData.LoadFromSavedData"/> when the key is missing
    /// from master data, filling defaults — ring <c>ID</c> often stays 0 so every bad JSON key became the same pickup
    /// (e.g. Worn Crest Ring). Only accept keys that exist in <see cref="GDEDataManager"/> master data.
    /// </summary>
    private static bool GdeMasterRowExists(string key)
    {
        key = NormalizeDropKey(key);
        if (string.IsNullOrEmpty(key))
            return false;
        try
        {
            Dictionary<string, object> dict;
            return GDEDataManager.Get(key, out dict) && dict != null;
        }
        catch
        {
            return false;
        }
    }

    private static int TryResolveRingIdByGdeRow(string key)
    {
        key = NormalizeDropKey(key);
        if (string.IsNullOrEmpty(key))
            return -1;
        // Player empty ring slot — not a distinct loot row for drops.
        if (string.Equals(key, "non_frame", StringComparison.OrdinalIgnoreCase))
            return -1;
        if (!GdeMasterRowExists(key))
            return -1;

        if (s_ringIdByKeyRuntime.TryGetValue(key, out int cached))
            return cached;

        try
        {
            GDEcrestringData row = new GDEcrestringData(key);
            int id = row.ID;
            if (id < 0)
                return -1;
            s_ringIdByKeyRuntime[key] = id;
            return id;
        }
        catch
        {
            return -1;
        }
    }

    private static int TryResolveUseIdByGdeRow(string key)
    {
        key = NormalizeDropKey(key);
        if (string.IsNullOrEmpty(key))
            return -1;
        if (!GdeMasterRowExists(key))
            return -1;

        if (s_useIdByKeyRuntime.TryGetValue(key, out int cached))
            return cached;

        try
        {
            GDEUseItemData row = new GDEUseItemData(key);
            int id = row.ID;
            if (id < 0)
                return -1;
            s_useIdByKeyRuntime[key] = id;
            return id;
        }
        catch
        {
            return -1;
        }
    }

    private static int TryResolveMagicIdByGdeRow(string key)
    {
        key = NormalizeDropKey(key);
        if (string.IsNullOrEmpty(key))
            return -1;
        if (!GdeMasterRowExists(key))
            return -1;

        if (s_magicIdByKeyRuntime.TryGetValue(key, out int cached))
            return cached;

        try
        {
            GDEmagicData row = new GDEmagicData(key);
            int id = row.ID;
            if (id < 0)
                return -1;
            s_magicIdByKeyRuntime[key] = id;
            return id;
        }
        catch
        {
            return -1;
        }
    }

}
