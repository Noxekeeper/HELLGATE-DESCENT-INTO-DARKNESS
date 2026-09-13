namespace NoREroMod.Systems.Spawn;

/// <summary>F11 Gold picker: fixed and ranged pile presets written as GOLD / gold= lines.</summary>
internal static class SpawnAuthoringGoldCatalog
{
    private static readonly string[] Presets =
    {
        "1-3",
        "2-5",
        "3-7",
        "5-10",
        "100",
        "200",
        "300",
        "400",
        "500"
    };

    internal static string[] GetKeys()
    {
        return Presets;
    }

    /// <summary>Normalize a picker key to the pack range token (<c>100</c> or <c>100-300</c>).</summary>
    internal static string NormalizeRangeToken(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        string token = key.Trim();
        if (token.StartsWith("gold=", System.StringComparison.OrdinalIgnoreCase))
            token = token.Substring("gold=".Length).Trim();
        return token;
    }
}
