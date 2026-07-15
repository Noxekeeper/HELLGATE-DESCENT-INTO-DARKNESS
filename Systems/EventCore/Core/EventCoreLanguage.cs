using System;
using System.IO;
using NoREroMod;

namespace NoREroMod.Systems.EventCore.Core;

/// <summary>
/// Maps <see cref="Plugin.hellGateLanguage"/> to language subfolders under the EventCore content root.
/// </summary>
internal static class EventCoreLanguage
{
    internal static string ResolveFolderCode()
    {
        try
        {
            string raw = Plugin.hellGateLanguage?.Value;
            if (raw == null || raw.Trim().Length == 0)
                return "Ru";

            switch (raw.Trim().ToUpperInvariant())
            {
                case "EN": return "En";
                case "RU": return "Ru";
                case "JP": return "Jp";
                case "CN": return "Cn";
                case "KR": return "Kr";
                case "FR": return "Fr";
                case "DE": return "De";
                case "PT": return "Pt";
                case "BR": return "Br";
                case "ES": return "Es";
                default:
                    return "Ru";
            }
        }
        catch
        {
            return "Ru";
        }
    }

    /// <summary>
    /// Resolves the active language pack path.
    /// EventCore language content is expected to live under <c>{Lang}/Stranger/eventcore_lang.json</c>.
    /// </summary>
    internal static string GetLanguagePackPath(string jsonRoot)
    {
        string folder = ResolveFolderCode();
        return Path.Combine(Path.Combine(Path.Combine(jsonRoot, folder), "Stranger"), "eventcore_lang.json");
    }

    private static readonly string[] StepFileLangFallbackOrder =
    {
        "Ru", "En", "Jp", "Cn", "Kr", "Fr", "De", "Pt", "Br", "Es"
    };

    /// <summary>
    /// Resolves a step file from the event manifest to <c>{Lang}/&lt;relative path&gt;</c>.
    /// Examples: <c>Stranger/eventcore_broker_gate_s1.json</c>,
    /// <c>FactionSocial/bandits/sex_paid/eventcore_fsp_bandits_sex_paid_s01.json</c>.
    /// </summary>
    internal static string ResolveStepFilePath(string jsonRoot, string relativeFromManifest)
    {
        if (string.IsNullOrEmpty(jsonRoot) || string.IsNullOrEmpty(relativeFromManifest))
            return string.Empty;

        string relative = relativeFromManifest.Trim().Replace('\\', '/');
        string active = ResolveFolderCode();

        if (TryCombineLangRelative(jsonRoot, active, relative, out string preferred))
            return preferred;

        for (int i = 0; i < StepFileLangFallbackOrder.Length; i++)
        {
            string lang = StepFileLangFallbackOrder[i];
            if (string.Equals(lang, active, StringComparison.OrdinalIgnoreCase))
                continue;
            if (TryCombineLangRelative(jsonRoot, lang, relative, out string fallback))
                return fallback;
        }

        // Legacy: manifest listed only filename under Stranger/
        const string strangerSegment = "Stranger/";
        if (relative.StartsWith(strangerSegment, StringComparison.OrdinalIgnoreCase))
        {
            string fileOnly = relative.Substring(strangerSegment.Length);
            if (TryCombineLangRelative(jsonRoot, active, Path.Combine("Stranger", fileOnly).Replace('\\', '/'), out preferred))
                return preferred;
        }

        return Path.Combine(jsonRoot, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool TryCombineLangRelative(string jsonRoot, string langFolder, string relativeUnderLang, out string fullPath)
    {
        string normalized = relativeUnderLang.Trim().Replace('\\', '/');
        string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        fullPath = Path.Combine(jsonRoot, langFolder);
        for (int i = 0; i < segments.Length; i++)
            fullPath = Path.Combine(fullPath, segments[i]);
        return File.Exists(fullPath);
    }
}
