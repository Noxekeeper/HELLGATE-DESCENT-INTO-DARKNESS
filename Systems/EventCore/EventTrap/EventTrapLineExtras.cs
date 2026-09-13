using System;
using System.Globalization;
using System.Text;

namespace NoREroMod.Systems.EventCore.EventTrap;

/// <summary>
/// Optional tokens on <c>EVENTTRAP</c> lines, applied on top of pack <c>config.json</c>.
/// Example: <c>EVENTTRAP,etrap_undead,12.00,-4.50,count=1-2,dist=6,8,sides=both,faction=undead,max=3,r=8,delay=1</c>
/// </summary>
internal sealed class EventTrapLineExtras
{
    internal bool HasCount;
    internal int CountMin;
    internal int CountMax;

    internal bool HasDistances;
    internal string DistancesCsv = string.Empty;

    internal bool HasSides;
    internal bool RightOnly;

    internal bool HasFaction;
    internal string FactionIdRaw = string.Empty;

    internal bool HasMaxWaves;
    internal int MaxWaves;

    internal bool HasRadius;
    internal float Radius;

    internal bool HasDelay;
    internal float DelaySeconds;

    internal bool HasAny =>
        HasCount || HasDistances || HasSides || HasFaction || HasMaxWaves || HasRadius || HasDelay;

    internal static EventTrapLineExtras FromAuthoring(
        string countStr,
        string distStr,
        int sidesIndex,
        string factionToken,
        string maxStr,
        string radiusStr,
        string delayStr)
    {
        var extras = new EventTrapLineExtras();
        TryParseCountToken(countStr, extras);
        TryParseDistToken(distStr, extras);

        if (sidesIndex == 1)
        {
            extras.HasSides = true;
            extras.RightOnly = false;
        }
        else if (sidesIndex == 2)
        {
            extras.HasSides = true;
            extras.RightOnly = true;
        }

        if (!string.IsNullOrEmpty(factionToken) && factionToken.Trim().Length > 0)
        {
            extras.HasFaction = true;
            extras.FactionIdRaw = factionToken.Trim();
        }

        if (TryParsePositiveInt(maxStr, out int maxWaves))
        {
            extras.HasMaxWaves = true;
            extras.MaxWaves = maxWaves;
        }

        if (TryParsePositiveFloat(radiusStr, out float radius))
        {
            extras.HasRadius = true;
            extras.Radius = radius;
        }

        if (TryParseNonNegativeFloat(delayStr, out float delay))
        {
            extras.HasDelay = true;
            extras.DelaySeconds = delay;
        }

        return extras;
    }

    internal static bool TryParse(string extrasRaw, out EventTrapLineExtras extras)
    {
        extras = new EventTrapLineExtras();
        if (string.IsNullOrEmpty(extrasRaw))
            return false;

        string[] tokens = extrasRaw.Split(',');
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i] != null ? tokens[i].Trim() : string.Empty;
            if (token.Length == 0)
                continue;

            int eq = token.IndexOf('=');
            if (eq <= 0)
            {
                if (extras.HasDistances && IsBareNumber(token))
                    extras.DistancesCsv = extras.DistancesCsv + "," + token;
                continue;
            }

            if (eq >= token.Length - 1)
                continue;

            string key = token.Substring(0, eq).Trim();
            string value = token.Substring(eq + 1).Trim();
            if (string.Equals(key, "count", StringComparison.OrdinalIgnoreCase))
                TryParseCountToken(value, extras);
            else if (string.Equals(key, "dist", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(key, "dists", StringComparison.OrdinalIgnoreCase))
                TryParseDistToken(value, extras);
            else if (string.Equals(key, "sides", StringComparison.OrdinalIgnoreCase))
                TryParseSidesToken(value, extras);
            else if (string.Equals(key, "faction", StringComparison.OrdinalIgnoreCase))
            {
                extras.HasFaction = true;
                extras.FactionIdRaw = value;
            }
            else if (string.Equals(key, "max", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParsePositiveInt(value, out int maxWaves))
                {
                    extras.HasMaxWaves = true;
                    extras.MaxWaves = maxWaves;
                }
            }
            else if (string.Equals(key, "r", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(key, "zone", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParsePositiveFloat(value, out float radius))
                {
                    extras.HasRadius = true;
                    extras.Radius = radius;
                }
            }
            else if (string.Equals(key, "delay", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseNonNegativeFloat(value, out float delay))
                {
                    extras.HasDelay = true;
                    extras.DelaySeconds = delay;
                }
            }
        }

        return extras.HasAny;
    }

    internal void ApplyTo(EventTrapConfigFile cfg)
    {
        if (cfg == null || !HasAny)
            return;

        if (HasCount)
        {
            cfg.spawnCountMin = CountMin;
            cfg.spawnCountMax = CountMax;
        }

        if (HasDistances)
            cfg.horizontalSpawnDistancesCsv = DistancesCsv;

        if (HasSides)
            cfg.spawnRightOnly = RightOnly;

        if (HasFaction)
            cfg.spawnFactionIdRaw = FactionIdRaw ?? string.Empty;

        if (HasMaxWaves)
            cfg.maxAmbushSpawns = MaxWaves;

        if (HasRadius)
        {
            cfg.suspicionEnterRadius = Radius;
            cfg.ambushZoneRadius = Radius;
        }

        if (HasDelay)
            cfg.ambushSpawnDelaySeconds = DelaySeconds;

        // Count / sides only affect the player-relative path. If the pack was
        // twin/flank-only, seed a distance so those overrides actually apply.
        bool needsPlayerRelative = HasCount || HasSides || HasDistances;
        if (needsPlayerRelative && string.IsNullOrEmpty(cfg.horizontalSpawnDistancesCsv))
        {
            if (cfg.ambushSideOffset > 0.01f)
                cfg.horizontalSpawnDistancesCsv = MathfRoundCsv(cfg.ambushSideOffset);
            else
                cfg.horizontalSpawnDistancesCsv = "6";
        }
    }

    internal string ToLineSuffix()
    {
        if (!HasAny)
            return string.Empty;

        var sb = new StringBuilder();
        if (HasCount)
        {
            sb.Append(",count=");
            if (CountMin == CountMax)
                sb.Append(CountMin.ToString(CultureInfo.InvariantCulture));
            else
                sb.Append(CountMin.ToString(CultureInfo.InvariantCulture))
                    .Append('-')
                    .Append(CountMax.ToString(CultureInfo.InvariantCulture));
        }

        if (HasDistances && !string.IsNullOrEmpty(DistancesCsv))
            sb.Append(",dist=").Append(DistancesCsv.Replace(',', ';'));

        if (HasSides)
            sb.Append(RightOnly ? ",sides=right" : ",sides=both");

        if (HasFaction && !string.IsNullOrEmpty(FactionIdRaw))
            sb.Append(",faction=").Append(FactionIdRaw.Trim());

        if (HasMaxWaves)
            sb.Append(",max=").Append(MaxWaves.ToString(CultureInfo.InvariantCulture));

        if (HasRadius)
            sb.Append(",r=").Append(FormatFloat(Radius));

        if (HasDelay)
            sb.Append(",delay=").Append(FormatFloat(DelaySeconds));

        return sb.ToString();
    }

    private static void TryParseCountToken(string raw, EventTrapLineExtras extras)
    {
        if (string.IsNullOrEmpty(raw))
            return;

        string value = raw.Trim();
        int dash = value.IndexOf('-');
        if (dash > 0 && dash < value.Length - 1)
        {
            if (!int.TryParse(value.Substring(0, dash).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int min))
                return;
            if (!int.TryParse(value.Substring(dash + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int max))
                return;
            if (min < 1)
                min = 1;
            if (max < min)
                max = min;
            extras.HasCount = true;
            extras.CountMin = min;
            extras.CountMax = max;
            return;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 1)
            return;
        extras.HasCount = true;
        extras.CountMin = n;
        extras.CountMax = n;
    }

    private static void TryParseDistToken(string raw, EventTrapLineExtras extras)
    {
        if (string.IsNullOrEmpty(raw))
            return;

        string cleaned = raw.Trim().Replace(';', ',');
        if (cleaned.Length == 0)
            return;

        extras.HasDistances = true;
        extras.DistancesCsv = cleaned;
    }

    private static void TryParseSidesToken(string raw, EventTrapLineExtras extras)
    {
        if (string.IsNullOrEmpty(raw))
            return;

        extras.HasSides = true;
        extras.RightOnly =
            string.Equals(raw, "right", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "rightonly", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBareNumber(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;
        float unused;
        return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out unused);
    }

    private static bool TryParsePositiveInt(string raw, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(raw))
            return false;
        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    private static bool TryParsePositiveFloat(string raw, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(raw))
            return false;
        return float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0.01f;
    }

    private static bool TryParseNonNegativeFloat(string raw, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(raw))
            return false;
        return float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0f;
    }

    private static string FormatFloat(float value)
    {
        if (Math.Abs(value - (float)Math.Round(value)) < 0.001f)
            return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string MathfRoundCsv(float value)
    {
        return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
    }
}
