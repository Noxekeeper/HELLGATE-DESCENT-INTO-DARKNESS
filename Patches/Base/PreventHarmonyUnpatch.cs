using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace NoREroMod.Patches.Base
{
    /// <summary>
    /// Blocks Harmony.UnpatchAll() calls in original NoREroMod plugins,
    /// otherwise they remove our patches right after scene load.
    /// </summary>
    internal static class PreventHarmonyUnpatch
    {
        // Patch all OnDestroy methods of NoREroMod.Plugin types from other assemblies
        private static IEnumerable<MethodBase> TargetMethods()
        {
            const string typeFullName = "NoREroMod.Plugin";

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Skip our own assembly (otherwise we would block our own onDestroy hooks if any)
                if (assembly == typeof(PreventHarmonyUnpatch).Assembly)
                {
                    continue;
                }

                var type = assembly.GetType(typeFullName, throwOnError: false);
                if (type == null)
                {
                    continue;
                }

                var method = AccessTools.DeclaredMethod(type, "OnDestroy");
                if (method != null)
                {
                    yield return method;
                }
            }
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            Plugin.Log?.LogWarning("[HarmonyGuard] Blocked Harmony.UnpatchAll() call in NoREroMod.Plugin.OnDestroy");
            return false; // skip original method to avoid calling UnpatchAll()
        }
    }
}

