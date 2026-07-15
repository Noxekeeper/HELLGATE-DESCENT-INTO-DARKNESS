using System;
using System.Reflection;

namespace NoREroMod;

/// <summary>
/// Resolves types without Harmony AccessTools.TypeByName — that scans all assemblies
/// via GetTypes() and spams warnings on XUnity.AutoTranslator with Unity 5.6.
/// </summary>
internal static class HellGateTypeResolver
{
    private static readonly Assembly GameAssembly = typeof(EnemyDate).Assembly;
    private static readonly Assembly NorAssembly = typeof(StruggleSystem).Assembly;
    private static readonly Assembly SelfAssembly = typeof(Plugin).Assembly;

    internal static Type Resolve(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        Type t = GameAssembly.GetType(typeName, false);
        if (t != null)
            return t;

        t = NorAssembly.GetType(typeName, false);
        if (t != null)
            return t;

        t = SelfAssembly.GetType(typeName, false);
        if (t != null)
            return t;

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Assembly asm = assemblies[i];
            if (asm == null || asm == GameAssembly || asm == NorAssembly || asm == SelfAssembly)
                continue;

            if (ShouldSkipAssembly(asm))
                continue;

            try
            {
                t = asm.GetType(typeName, false);
                if (t != null)
                    return t;
            }
            catch
            {
            }
        }

        return null;
    }

    private static bool ShouldSkipAssembly(Assembly asm)
    {
        string name = asm.GetName().Name;
        if (string.IsNullOrEmpty(name))
            return false;

        return name.StartsWith("XUnity.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("BepInEx", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Harmony", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Mono.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("System", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase);
    }
}
