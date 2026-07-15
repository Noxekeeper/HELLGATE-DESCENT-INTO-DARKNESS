using System;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Gameplay;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Resolves the seed source faction for a creampie. Many H-scenes invoke
/// <c>EnemyDate.Nakadasi</c> on an ERO-only object (<c>Mutudeero</c>, <c>goblinero</c>,
/// <c>TrapSpiderERO</c>, slime, custom wolf, …) that was never registered in
/// <see cref="EnemyFactionRuntime"/>, so a plain <c>GetFaction</c> returns Neutral.
///
/// Resolution order (first non-neutral wins):
///   1. The instance's own / root GameObject faction (registered combat enemies).
///   2. The active H-scene enemy from <see cref="QTESystem"/> (same source the reputation bridge uses).
///   3. Type-name classification: the game's Factions.json type lists, then an ERO-aware
///      normalization, then a substring heuristic that covers slimes/traps/beasts.
/// </summary>
internal static class PregnancySourceResolver
{
    public static int Resolve(EnemyDate instance, out string diag)
    {
        diag = null;

        // 1. Registered runtime instance.
        int faction = SafeGetFaction(instance != null ? instance.gameObject : null);
        if (faction != FactionIds.Neutral) { diag = "instance"; return faction; }

        if (instance != null && instance.transform != null && instance.transform.root != null &&
            instance.transform.root.gameObject != instance.gameObject)
        {
            faction = SafeGetFaction(instance.transform.root.gameObject);
            if (faction != FactionIds.Neutral) { diag = "root"; return faction; }
        }

        // 2. Active H-scene enemy (resolves the combat instance even when Nakadasi fired on an ERO object).
        object hsceneEnemy = null;
        try { hsceneEnemy = QTESystem.GetCurrentEnemyInstance(); } catch { }
        GameObject hsceneGo = ExtractGameObject(hsceneEnemy);
        if (hsceneGo != null)
        {
            faction = SafeGetFaction(hsceneGo);
            if (faction != FactionIds.Neutral) { diag = "qte"; return faction; }
        }

        // 3. Type-name classification across every name we can see.
        string instanceType = instance != null ? instance.GetType().Name : null;
        string hsceneType = hsceneEnemy != null ? hsceneEnemy.GetType().Name : null;
        string instanceObjName = instance != null && instance.gameObject != null ? instance.gameObject.name : null;
        string hsceneObjName = hsceneGo != null ? hsceneGo.name : null;

        faction = ClassifyByName(instanceType);
        if (faction != FactionIds.Neutral) { diag = "type:" + instanceType; return faction; }
        faction = ClassifyByName(hsceneType);
        if (faction != FactionIds.Neutral) { diag = "qteType:" + hsceneType; return faction; }
        faction = ClassifyByName(instanceObjName);
        if (faction != FactionIds.Neutral) { diag = "obj:" + instanceObjName; return faction; }
        faction = ClassifyByName(hsceneObjName);
        if (faction != FactionIds.Neutral) { diag = "qteObj:" + hsceneObjName; return faction; }

        diag = $"UNRESOLVED inst='{instanceType}' qte='{hsceneType}' obj='{instanceObjName}'";
        return FactionIds.Neutral;
    }

    private static int SafeGetFaction(GameObject go)
    {
        if (go == null)
            return FactionIds.Neutral;
        try { return EnemyFactionRuntime.GetFaction(go); }
        catch { return FactionIds.Neutral; }
    }

    private static GameObject ExtractGameObject(object instance)
    {
        if (instance is GameObject go)
            return go;
        if (instance is Component component)
            return component != null ? component.gameObject : null;
        return null;
    }

    /// <summary>
    /// Maps a raw runtime/type/prefab name to a faction: exact config lists first
    /// (most accurate), then ERO-normalized config lists, then a substring heuristic.
    /// </summary>
    private static int ClassifyByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return FactionIds.Neutral;

        string clean = name.Replace("(Clone)", "").Trim();

        int f = SafeResolveByType(clean);
        if (f != FactionIds.Neutral)
            return f;

        string normalized = StripEroAffixes(clean);
        if (!string.Equals(normalized, clean, StringComparison.OrdinalIgnoreCase))
        {
            f = SafeResolveByType(normalized);
            if (f != FactionIds.Neutral)
                return f;
        }

        return ClassifyBySubstring(clean.ToLowerInvariant());
    }

    private static int SafeResolveByType(string typeName)
    {
        try { return EnemyFactionRuntime.ResolveFactionByTypeName(typeName); }
        catch { return FactionIds.Neutral; }
    }

    private static string StripEroAffixes(string name)
    {
        string n = name;
        string[] suffixes = { "ero2", "ero", "Start" };
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < suffixes.Length; i++)
            {
                string s = suffixes[i];
                if (n.Length > s.Length && n.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                {
                    n = n.Substring(0, n.Length - s.Length);
                    changed = true;
                }
            }
        }
        string[] prefixes = { "Start", "Ero" };
        changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < prefixes.Length; i++)
            {
                string p = prefixes[i];
                if (n.Length > p.Length && n.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                {
                    n = n.Substring(p.Length);
                    changed = true;
                }
            }
        }
        return n;
    }

    private static int ClassifyBySubstring(string lower)
    {
        if (Has(lower, "mafia", "tyoukyoushi"))
            return FactionIds.Mafia;
        if (Has(lower, "inquisi", "pilgrim", "sister", "praymaiden", "coolmaiden", "crow", "angel"))
            return FactionIds.Church;
        if (Has(lower, "mummy", "undead", "skel", "crawlingdead", "cocoonman", "coccon"))
            return FactionIds.Undead;
        if (Has(lower, "mutude", "goblin", "succubus", "sheephead", "minotau", "demon", "requiem", "merman"))
            return FactionIds.Demons;
        if (Has(lower, "touzoku", "vagrant", "gorotuki", "kakash", "kakasi", "dorei", "slave", "bandit"))
            return FactionIds.Bandits;
        if (Has(lower, "suraimu", "slime", "ooze", "snail", "mushroom", "kinoko", "mimic", "mimick",
                       "arulaune", "ivy", "rosewarm", "spider", "tentacle", "wolf", "biscod"))
            return FactionIds.Monsters;
        return FactionIds.Neutral;
    }

    private static bool Has(string haystack, params string[] needles)
    {
        for (int i = 0; i < needles.Length; i++)
        {
            if (haystack.IndexOf(needles[i], StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }
}
