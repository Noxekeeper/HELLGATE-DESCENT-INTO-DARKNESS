using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using NoREroMod;
using Spine.Unity;

namespace NoREroMod.Patches.Enemy
{
    /// <summary>
    /// Disables the collision-based Elite Grab from NoREroMod when DisableOriginalEliteGrab is true.
    /// Patches all CanEliteGrabPlayer overloads to return false, and overrides
    /// the red elite tint back to white so the visual indicator is hidden.
    /// </summary>
    internal static class NoREroModEliteGrabDisablerPatch
    {
        private static bool _applied;

        public static void Apply(Harmony harmony)
        {
            if (_applied) return;

            try
            {
                var norAssembly = typeof(StruggleSystem).Assembly;
                var type = norAssembly.GetType("NoREroMod.EnemyDatePatch");
                if (type == null)
                {
                    Plugin.Log?.LogWarning("[GrabSystemNG] NoREroMod.EnemyDatePatch not found");
                    return;
                }

                var prefix = typeof(NoREroModEliteGrabDisablerPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefix == null) return;

                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => m.Name == "CanEliteGrabPlayer")
                    .ToList();

                int patched = 0;
                foreach (var m in methods)
                {
                    try
                    {
                        harmony.Patch(m, new HarmonyMethod(prefix));
                        patched++;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning($"[GrabSystemNG] CanEliteGrabPlayer({m.GetParameters().Length} params) patch failed: {ex.Message}");
                    }
                }

                if (patched > 0)
                {
                    _applied = true;
                    Plugin.Log?.LogInfo($"[GrabSystemNG] Patched {patched} CanEliteGrabPlayer overload(s)");
                }
                else
                {
                    Plugin.Log?.LogWarning("[GrabSystemNG] CanEliteGrabPlayer not found — collision grab will NOT be disabled");
                }

                PatchEliteColorOverride(harmony, type, norAssembly);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[GrabSystemNG] NoREroModEliteGrabDisabler failed: {ex.Message}");
            }
        }

        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            if (Plugin.disableOriginalEliteGrab?.Value ?? true)
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static void PatchEliteColorOverride(Harmony harmony, Type enemyDatePatchType, Assembly norAssembly)
        {
            if (!(Plugin.disableOriginalEliteGrab?.Value ?? true)) return;
            try
            {
                var colorMethod = enemyDatePatchType.GetMethod("SuperEnemyColor", BindingFlags.NonPublic | BindingFlags.Static);
                if (colorMethod != null)
                {
                    var postfix = typeof(NoREroModEliteGrabDisablerPatch).GetMethod("SuperEnemyColor_Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                    if (postfix != null) harmony.Patch(colorMethod, postfix: new HarmonyMethod(postfix));
                }

                var spawnMethod = enemyDatePatchType.GetMethod("SpawnSuperEnemyAndHideHP", BindingFlags.NonPublic | BindingFlags.Static);
                if (spawnMethod != null)
                {
                    var spawnPostfix = typeof(NoREroModEliteGrabDisablerPatch).GetMethod("SpawnSuperEnemy_Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                    if (spawnPostfix != null) harmony.Patch(spawnMethod, postfix: new HarmonyMethod(spawnPostfix));
                }

                var uimngType = norAssembly.GetType("NoREroMod.UImngPatch");
                var updateColorMethod = uimngType?.GetMethod("UpdateGrabStateWithColor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (updateColorMethod != null)
                {
                    var p = typeof(NoREroModEliteGrabDisablerPatch).GetMethod("UpdateGrabStateWithColor_Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                    if (p != null) harmony.Patch(updateColorMethod, prefix: new HarmonyMethod(p));
                }

                PatchEnemyUpdateForColor(harmony);
            }
            catch (Exception ex) { Plugin.Log?.LogWarning($"[GrabSystemNG] Elite color override: {ex.Message}"); }
        }

        private static readonly string[] _eliteUpdateTypes = new[]
        {
            "TouzokuNormal", "TouzokuAxe", "MummyDog", "MummyMan", "goblin", "Arulaune", "BigMerman", "Bigoni",
            "BlackMafia", "BlackOoze_Monster", "Cocoonman", "Coolmaiden", "CrawlingCreatures", "CrawlingDead",
            "CrawlingSisterKnight", "CrowInquisition", "Gorotuki", "Inquisition", "InquisitionRED", "InquisitionWhite",
            "Kakash", "Kinoko", "Librarian", "Mafiamuscle", "Minotaurosu", "Mutude", "Pilgrim", "PrisonOfficer",
            "RequiemKnight", "Sheepheaddemon", "Sisiruirui", "Sisterknight", "SkeltonOoze", "Slaughterer",
            "SlaveBigAxe", "Snailshell", "SuccubusSpine", "Tyoukyoushi", "TyoukyoushiRed", "Undead", "Vagrant"
        };

        private static void PatchEnemyUpdateForColor(Harmony harmony)
        {
            var gameAssembly = typeof(EnemyDate).Assembly;
            var postfix = typeof(NoREroModEliteGrabDisablerPatch).GetMethod("EnemyUpdate_Postfix", BindingFlags.Static | BindingFlags.NonPublic);
            if (postfix == null) return;
            var hm = new HarmonyMethod(postfix) { priority = 4000 };
            foreach (var typeName in _eliteUpdateTypes)
            {
                var t = gameAssembly.GetType(typeName);
                if (t == null) continue;
                var update = t.GetMethod("Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (update != null) try { harmony.Patch(update, postfix: hm); } catch { }
            }
        }

        [HarmonyPostfix]
        private static void SuperEnemyColor_Postfix(EnemyDate __instance) => OverrideEliteColorToWhite(__instance);

        [HarmonyPostfix]
        private static void SpawnSuperEnemy_Postfix(EnemyDate __instance) => OverrideEliteColorToWhite(__instance);

        [HarmonyPrefix]
        private static bool UpdateGrabStateWithColor_Prefix(EnemyDate __instance)
        {
            if (__instance != null) OverrideEliteColorToWhite(__instance);
            return false;
        }

        [HarmonyPostfix]
        private static void EnemyUpdate_Postfix(EnemyDate __instance) => OverrideEliteColorToWhite(__instance);

        private static void OverrideEliteColorToWhite(EnemyDate enemy)
        {
            if (!(Plugin.disableOriginalEliteGrab?.Value ?? true) || enemy == null) return;
            var jpName = Traverse.Create(enemy).Field("JPname").GetValue() as string;
            if (string.IsNullOrEmpty(jpName) || !jpName.Contains("<SUPER>")) return;
            try
            {
                foreach (var spine in enemy.GetComponentsInChildren<SkeletonAnimation>())
                {
                    if (spine?.skeleton != null) spine.skeleton.SetColor(Color.white);
                }
            }
            catch { }
        }
    }
}
