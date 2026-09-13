using System.Collections.Generic;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>Registered combat-fatality profiles (one entry per enemy family).</summary>
internal static class EnemyFatalityRegistry
{
    private static readonly List<IEnemyFatalityProfile> Profiles = new List<IEnemyFatalityProfile>(8);

    internal static List<IEnemyFatalityProfile> All => Profiles;

    internal static void Register(IEnemyFatalityProfile profile)
    {
        if (profile == null)
            return;

        for (int i = 0; i < Profiles.Count; i++)
        {
            if (string.Equals(Profiles[i].Id, profile.Id, System.StringComparison.Ordinal))
            {
                Profiles[i] = profile;
                return;
            }
        }

        Profiles.Add(profile);
    }

    internal static bool AnyModuleEnabled()
    {
        for (int i = 0; i < Profiles.Count; i++)
        {
            if (Profiles[i].IsModuleEnabled)
                return true;
        }

        return false;
    }

    /// <summary>First enabled body-hit profile that matches (skips magic-projectile-only).</summary>
    internal static IEnemyFatalityProfile FindEnabledMatch(EnemyDate enemy)
    {
        if (enemy == null)
            return null;

        for (int i = 0; i < Profiles.Count; i++)
        {
            IEnemyFatalityProfile profile = Profiles[i];
            if (profile.RequiresMagicProjectile)
                continue;
            if (profile.IsEnabled && profile.Matches(enemy))
                return profile;
        }

        return null;
    }

    /// <summary>First enabled magic-projectile-only profile that matches.</summary>
    internal static IEnemyFatalityProfile FindEnabledMagicProjectileMatch(EnemyDate enemy)
    {
        if (enemy == null)
            return null;

        for (int i = 0; i < Profiles.Count; i++)
        {
            IEnemyFatalityProfile profile = Profiles[i];
            if (!profile.RequiresMagicProjectile)
                continue;
            if (profile.IsEnabled && profile.Matches(enemy))
                return profile;
        }

        return null;
    }
}
