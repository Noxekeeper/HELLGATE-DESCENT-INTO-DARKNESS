using System.Collections.Generic;

namespace NoREroMod.Systems.EventCore.Core;

/// <summary>Shared CSV parsing for EventCore encounter pack configs.</summary>
internal static class EventAnchorCsv
{
    internal static string[] ParseStrings(string csv)
    {
        if (string.IsNullOrEmpty(csv))
            return new string[0];
        string[] parts = csv.Split(',');
        var list = new List<string>();
        for (int i = 0; i < parts.Length; i++)
        {
            string s = parts[i].Trim();
            if (s.Length > 0)
                list.Add(s);
        }

        return list.ToArray();
    }

    internal static int[] ParseInts(string csv)
    {
        if (string.IsNullOrEmpty(csv))
            return new int[0];
        string[] parts = csv.Split(',');
        var list = new List<int>();
        for (int i = 0; i < parts.Length; i++)
        {
            string s = parts[i].Trim();
            if (int.TryParse(s, out int v) && v > 0)
                list.Add(v);
        }

        return list.ToArray();
    }
}
