using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions;

internal static class FactionStyle
{
    private static readonly string[] TouzokuBoneCandidates = { "bone6", "bone27", "head", "neck", "body_ue", "body", "hips", "root" };
    private static readonly string[] VagrantBoneCandidates = { "bone7", "bone6", "bone27", "head", "neck", "body_ue", "body", "hips", "root" };
    private static readonly string[] MutudeBoneCandidates = { "bone16", "bone6", "bone27", "head", "neck", "body_ue", "body", "hips", "root" };

    internal sealed class IconStyle
    {
        public Sprite Icon;
        public float Scale;
        public float OffsetX;
        public float OffsetY;
        public string[] PreferredBones;
    }

    private static readonly Dictionary<int, IconStyle> _stylesByFaction = new Dictionary<int, IconStyle>();
    private static bool _stylesLoaded;

    public static bool TryGetIconStyle(int factionId, out IconStyle style)
    {
        style = null;
        EnsureStylesLoaded();
        return _stylesByFaction.TryGetValue(factionId, out style);
    }

    private static void EnsureStylesLoaded()
    {
        if (_stylesLoaded)
            return;
        _stylesLoaded = true;

        string baseDir = ResolveBanditsDirectory();
        TryRegister(FactionIds.Bandits, Path.Combine(baseDir, "bandits_faction.png"));
        TryRegister(FactionIds.BanditsMafiaLoyal, Path.Combine(baseDir, "bandits_mafia.png"));
        TryRegister(FactionIds.BanditsDemonsLoyal, Path.Combine(baseDir, "bandits_demons.png"));
        TryRegister(FactionIds.BanditsInquisitionLoyal, Path.Combine(baseDir, "bandits_inquisition.png"));

        string demonsDir = ResolveDemonsDirectory();
        TryRegister(FactionIds.Demons, Path.Combine(demonsDir, "Demons.png"));

        string churchDir = ResolveChurchDirectory();
        string churchIconPath = ResolveChurchIconPath(churchDir);
        TryRegister(FactionIds.Church, churchIconPath);

        string mafiaDir = ResolveMafiaDirectory();
        TryRegister(FactionIds.Mafia, Path.Combine(mafiaDir, "Mafia.png"));

        string undeadDir = ResolveUndeadDirectory();
        TryRegister(FactionIds.Undead, Path.Combine(undeadDir, "Undead.png"));

        string monstersDir = ResolveMonstersDirectory();
        TryRegister(FactionIds.Monsters, Path.Combine(monstersDir, "monsters.png"));

        string witchDir = ResolveWitchDirectory();
        TryRegister(FactionIds.Witch, Path.Combine(witchDir, "WitchFactionLogo.png"));
    }

    private static void TryRegister(int factionId, string filePath)
    {
        Sprite icon = LoadSpriteFromPng(filePath);
        if (icon == null)
        {
            Plugin.Log?.LogWarning("[FactionStyle] Missing/invalid icon: " + filePath);
            return;
        }

        _stylesByFaction[factionId] = new IconStyle
        {
            Icon = icon,
            Scale = 0.95f,
            OffsetX = 0f,
            OffsetY = -0.25f,
            PreferredBones = null
        };
    }

    public static IconStyle ResolveForEnemy(EnemyDate enemy, IconStyle baseStyle)
    {
        if (baseStyle == null)
            return null;

        var resolved = new IconStyle
        {
            Icon = baseStyle.Icon,
            Scale = baseStyle.Scale,
            OffsetX = baseStyle.OffsetX,
            OffsetY = baseStyle.OffsetY,
            PreferredBones = baseStyle.PreferredBones
        };
        if (enemy == null)
            return resolved;

        string typeName = enemy.GetType().Name;
        if (string.Equals(typeName, "TouzokuNormal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, "TouzokuAxe", StringComparison.OrdinalIgnoreCase))
        {
            // User-tuned alignment for Touzoku/TouzokuAxe only.
            resolved.OffsetY = 0f;
            resolved.PreferredBones = TouzokuBoneCandidates;
        }
        else if (string.Equals(typeName, "Vagrant", StringComparison.OrdinalIgnoreCase))
        {
            // Vagrant emblem anchor: prefer bone7.
            resolved.PreferredBones = VagrantBoneCandidates;
        }
        else if (string.Equals(typeName, "Mutude", StringComparison.OrdinalIgnoreCase))
        {
            // Mutude emblem anchor: prefer bone16.
            resolved.PreferredBones = MutudeBoneCandidates;
        }

        return resolved;
    }

    private static string ResolveBanditsDirectory()
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        var candidates = new List<string>(10);
        AddUniquePath(candidates, Combine5(gameRoot, "sources", "HellGate_sources", "Factions", "Bandits"));
        AddUniquePath(candidates, Combine8(gameRoot, "BepInEx", "plugins", "NoR_HellGate", "sources", "HellGate_sources", "Factions", "Bandits"));
        AddUniquePath(candidates, Combine5(Path.Combine(gameRoot, ".."), "sources", "HellGate_sources", "Factions", "Bandits"));

        try
        {
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(dllDir))
            {
                AddUniquePath(candidates, Combine5(dllDir, "sources", "HellGate_sources", "Factions", "Bandits"));
            }
        }
        catch { }

        for (int i = 0; i < candidates.Count; i++)
        {
            string dir = candidates[i];
            if (!Directory.Exists(dir))
                continue;
            if (File.Exists(Path.Combine(dir, "bandits_faction.png")))
                return dir;
        }

        return candidates.Count > 0 ? candidates[0] : Combine5(gameRoot, "sources", "HellGate_sources", "Factions", "Bandits");
    }

    private static string ResolveDemonsDirectory()
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        var candidates = new List<string>(10);
        AddUniquePath(candidates, Combine5(gameRoot, "sources", "HellGate_sources", "Factions", "Demons"));
        AddUniquePath(candidates, Combine8(gameRoot, "BepInEx", "plugins", "NoR_HellGate", "sources", "HellGate_sources", "Factions", "Demons"));
        AddUniquePath(candidates, Combine5(Path.Combine(gameRoot, ".."), "sources", "HellGate_sources", "Factions", "Demons"));

        try
        {
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(dllDir))
            {
                AddUniquePath(candidates, Combine5(dllDir, "sources", "HellGate_sources", "Factions", "Demons"));
            }
        }
        catch { }

        for (int i = 0; i < candidates.Count; i++)
        {
            string dir = candidates[i];
            if (!Directory.Exists(dir))
                continue;
            if (File.Exists(Path.Combine(dir, "Demons.png")))
                return dir;
        }

        return candidates.Count > 0 ? candidates[0] : Combine5(gameRoot, "sources", "HellGate_sources", "Factions", "Demons");
    }

    private static string ResolveChurchDirectory()
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        const string ChurchLatin = "Church";

        var candidates = new List<string>(8);
        AddUniquePath(candidates, Combine5(gameRoot, "sources", "HellGate_sources", "Factions", ChurchLatin));
        AddUniquePath(candidates, Combine8(gameRoot, "BepInEx", "plugins", "NoR_HellGate", "sources", "HellGate_sources", "Factions", ChurchLatin));
        AddUniquePath(candidates, Combine5(Path.Combine(gameRoot, ".."), "sources", "HellGate_sources", "Factions", ChurchLatin));

        try
        {
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(dllDir))
            {
                AddUniquePath(candidates, Combine5(dllDir, "sources", "HellGate_sources", "Factions", ChurchLatin));
            }
        }
        catch { }

        for (int i = 0; i < candidates.Count; i++)
        {
            string dir = candidates[i];
            if (!Directory.Exists(dir))
                continue;
            if (!string.IsNullOrEmpty(ResolveChurchIconPath(dir)))
                return dir;
        }

        return candidates.Count > 0 ? candidates[0] : Combine5(gameRoot, "sources", "HellGate_sources", "Factions", ChurchLatin);
    }

    private static string ResolveChurchIconPath(string churchDir)
    {
        if (string.IsNullOrEmpty(churchDir))
            return string.Empty;

        const string ChurchLatinFile = "Church.png";
        string latPath = Path.Combine(churchDir, ChurchLatinFile);
        if (File.Exists(latPath))
            return latPath;

        return latPath;
    }

    private static string ResolveMafiaDirectory()
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        const string MafiaLatin = "Mafia";

        var candidates = new List<string>(8);
        AddUniquePath(candidates, Combine5(gameRoot, "sources", "HellGate_sources", "Factions", MafiaLatin));
        AddUniquePath(candidates, Combine8(gameRoot, "BepInEx", "plugins", "NoR_HellGate", "sources", "HellGate_sources", "Factions", MafiaLatin));
        AddUniquePath(candidates, Combine5(Path.Combine(gameRoot, ".."), "sources", "HellGate_sources", "Factions", MafiaLatin));

        try
        {
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(dllDir))
            {
                AddUniquePath(candidates, Combine5(dllDir, "sources", "HellGate_sources", "Factions", MafiaLatin));
            }
        }
        catch { }

        for (int i = 0; i < candidates.Count; i++)
        {
            string dir = candidates[i];
            if (!Directory.Exists(dir))
                continue;
            if (File.Exists(Path.Combine(dir, "Mafia.png")))
                return dir;
        }

        return candidates.Count > 0 ? candidates[0] : Combine5(gameRoot, "sources", "HellGate_sources", "Factions", MafiaLatin);
    }

    private static string ResolveUndeadDirectory()
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        const string UndeadLatin = "Undead";

        var candidates = new List<string>(8);
        AddUniquePath(candidates, Combine5(gameRoot, "sources", "HellGate_sources", "Factions", UndeadLatin));
        AddUniquePath(candidates, Combine8(gameRoot, "BepInEx", "plugins", "NoR_HellGate", "sources", "HellGate_sources", "Factions", UndeadLatin));
        AddUniquePath(candidates, Combine5(Path.Combine(gameRoot, ".."), "sources", "HellGate_sources", "Factions", UndeadLatin));

        try
        {
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(dllDir))
            {
                AddUniquePath(candidates, Combine5(dllDir, "sources", "HellGate_sources", "Factions", UndeadLatin));
            }
        }
        catch { }

        for (int i = 0; i < candidates.Count; i++)
        {
            string dir = candidates[i];
            if (!Directory.Exists(dir))
                continue;
            if (File.Exists(Path.Combine(dir, "Undead.png")))
                return dir;
        }

        return candidates.Count > 0 ? candidates[0] : Combine5(gameRoot, "sources", "HellGate_sources", "Factions", UndeadLatin);
    }

    private static string ResolveMonstersDirectory()
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        const string MonstersLatin = "monsters";

        var candidates = new List<string>(8);
        AddUniquePath(candidates, Combine5(gameRoot, "sources", "HellGate_sources", "Factions", MonstersLatin));
        AddUniquePath(candidates, Combine8(gameRoot, "BepInEx", "plugins", "NoR_HellGate", "sources", "HellGate_sources", "Factions", MonstersLatin));
        AddUniquePath(candidates, Combine5(Path.Combine(gameRoot, ".."), "sources", "HellGate_sources", "Factions", MonstersLatin));

        try
        {
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(dllDir))
            {
                AddUniquePath(candidates, Combine5(dllDir, "sources", "HellGate_sources", "Factions", MonstersLatin));
            }
        }
        catch { }

        for (int i = 0; i < candidates.Count; i++)
        {
            string dir = candidates[i];
            if (!Directory.Exists(dir))
                continue;
            if (File.Exists(Path.Combine(dir, "monsters.png")))
                return dir;
        }

        return candidates.Count > 0 ? candidates[0] : Combine5(gameRoot, "sources", "HellGate_sources", "Factions", MonstersLatin);
    }

    private static string ResolveWitchDirectory()
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        const string WitchLatin = "WitchFaction";

        var candidates = new List<string>(8);
        AddUniquePath(candidates, Combine5(gameRoot, "sources", "HellGate_sources", "Factions", WitchLatin));
        AddUniquePath(candidates, Combine8(gameRoot, "BepInEx", "plugins", "NoR_HellGate", "sources", "HellGate_sources", "Factions", WitchLatin));
        AddUniquePath(candidates, Combine5(Path.Combine(gameRoot, ".."), "sources", "HellGate_sources", "Factions", WitchLatin));

        try
        {
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(dllDir))
            {
                AddUniquePath(candidates, Combine5(dllDir, "sources", "HellGate_sources", "Factions", WitchLatin));
            }
        }
        catch { }

        for (int i = 0; i < candidates.Count; i++)
        {
            string dir = candidates[i];
            if (!Directory.Exists(dir))
                continue;
            if (File.Exists(Path.Combine(dir, "WitchFactionLogo.png")))
                return dir;
        }

        return candidates.Count > 0 ? candidates[0] : Combine5(gameRoot, "sources", "HellGate_sources", "Factions", WitchLatin);
    }

    private static void AddUniquePath(List<string> paths, string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], full, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            paths.Add(full);
        }
        catch { }
    }

    private static string Combine5(string root, string a, string b, string c, string d)
    {
        return Path.Combine(Path.Combine(Path.Combine(Path.Combine(root, a), b), c), d);
    }

    private static string Combine8(string root, string a, string b, string c, string d, string e, string f, string g)
    {
        return Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(root, a), b), c), d), e), f), g);
    }

    private static Sprite LoadSpriteFromPng(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length == 0)
                return null;

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            if (!texture.LoadImage(bytes, false))
                return null;

            Rect rect = new Rect(0f, 0f, texture.width, texture.height);
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            return Sprite.Create(texture, rect, pivot, 100f);
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[FactionStyle] Failed loading sprite " + path + ": " + ex.Message);
            return null;
        }
    }
}
