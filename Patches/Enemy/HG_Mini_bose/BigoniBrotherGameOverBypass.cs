using System.Collections;
using System.Reflection;
using HarmonyLib;
using Spine;
using UnityEngine;
using NoREroMod;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Блокирует оригинальные Game Over механики Bigoni for BigoniBrother.
/// Сохраняет управление игроком after H-scene и not допускает загрузки сцены поражения.
/// </summary>
[HarmonyPatch]
internal static class BigoniBrotherGameOverBypass
{
    private static bool _brotherSceneActive;

    private static bool TryGetBigoni(StartBigoniERO instance, out Bigoni bigoni)
    {
        bigoni = null;
        if (instance == null)
        {
            return false;
        }

        var oyaField = typeof(StartBigoniERO).GetField("oya", BindingFlags.NonPublic | BindingFlags.Instance);
        bigoni = oyaField?.GetValue(instance) as Bigoni;
        return bigoni != null;
    }

    private static bool IsBigoniBrother(StartBigoniERO instance, out Bigoni bigoni)
    {
        if (!TryGetBigoni(instance, out bigoni))
        {
            return false;
        }

        var go = bigoni.gameObject;
        return go != null && !string.IsNullOrEmpty(go.name) && go.name.Contains("BigoniBrother");
    }

    private static void MarkSceneActive(StartBigoniERO instance)
    {
        _brotherSceneActive = true;
        EnsurePlayerReady(instance);
    }

    private static void MarkSceneInactive()
    {
        _brotherSceneActive = false;
    }

    /// <summary>
    /// Гарантирует, that игрок активен, физика включеon и борьба доступна.
    /// </summary>
    private static void EnsurePlayerReady(StartBigoniERO instance)
    {
        if (instance == null)
        {
            return;
        }

        var playerField = typeof(StartBigoniERO).GetField("player", BindingFlags.NonPublic | BindingFlags.Instance);
        var statusField = typeof(StartBigoniERO).GetField("pl", BindingFlags.NonPublic | BindingFlags.Instance);

        var player = playerField?.GetValue(instance) as playercon;
        var status = statusField?.GetValue(instance) as PlayerStatus;

        EnsurePlayerReady(player, status);
    }

    /// <summary>
    /// Перегрузка without StartBigoniERO — используется after передачи.
    /// </summary>
    private static void EnsurePlayerReady(playercon player)
    {
        PlayerStatus status = null;
        status = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetPlayerStatus();

        EnsurePlayerReady(player, status);
    }

    private static void EnsurePlayerReady(playercon player, PlayerStatus status)
    {
        if (player != null)
        {
            player.eroflag = false;
            player._eroflag2 = false;
            if (!player.gameObject.activeSelf)
            {
                player.gameObject.SetActive(true);
            }

            if (!player.enabled)
            {
                player.enabled = true;
            }

            if (player.rigi2d != null && !player.rigi2d.simulated)
            {
                player.rigi2d.simulated = true;
            }

            // If героиня лежит, оставляем nowdamage in true, so that state оставался DOWN
            player.nowdamage = player.erodown != 0;

            player.Attacknow = false;
            player.Actstate = false;
            player.stepfrag = false;
            player.magicnow = false;
            player.guard = false;

            var parryField = typeof(playercon).GetField("Parry", BindingFlags.NonPublic | BindingFlags.Instance);
            parryField?.SetValue(player, false);

            var itemUseField = typeof(playercon).GetField("Itemuse", BindingFlags.NonPublic | BindingFlags.Instance);
            itemUseField?.SetValue(player, false);

            var stabNowField = typeof(playercon).GetField("stabnow", BindingFlags.NonPublic | BindingFlags.Instance);
            stabNowField?.SetValue(player, false);

            player._easyESC = false;
            StruggleSystem.setStruggleLevel(-1f);
            Time.timeScale = 1f;
        }

        if (status != null)
        {
            status._SOUSA = true;
            status._SOUSAMNG = true;
        }
    }

    private static string GetEventName(Spine.Event e)
    {
        if (e?.Data == null)
        {
            return null;
        }

        // Spine 3.8 использует свойство name (with маленькой буквы)
        var data = e.Data;
        return data.Name;
    }

    /// <summary>
    /// Отслеживаем старт H-scene BigoniBrother и включаем игрока.
    /// </summary>
    [HarmonyPatch(typeof(StartBigoniERO), "OnEvent")]
    private static class TrackBrotherSceneOnStart
    {
        private static void Postfix(StartBigoniERO __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
        {
            if (!IsBigoniBrother(__instance, out _))
            {
                return;
            }

            string eventName = GetEventName(e);
            if (eventName == "START")
            {
                MarkSceneActive(__instance);
                // Plugin.Log.LogInfo( "[BIGONI BROTHER] H-scene START detected, keeping player active");
            }
        }
    }

    /// <summary>
    /// Reset flag after handoff of GG другому enemyу.
    /// </summary>
    [HarmonyPatch(typeof(BigoniBrotherPassLogic), nameof(BigoniBrotherPassLogic.ExecuteHandoff))]
    private static class ClearSceneFlagAfterHandoff
    {
        private static void Postfix()
        {
            var playerObj = GameObject.FindWithTag("Player");
            var player = playerObj != null ? playerObj.GetComponent<playercon>() : null;
            EnsurePlayerReady(player);
            MarkSceneInactive();
            // Plugin.Log.LogInfo( "[BIGONI BROTHER] H-scene finished, Game Over bypass active");
        }
    }

    /// <summary>
    /// Блокируем BigoniERO.Start, which готовит сцену Game Over.
    /// </summary>
    [HarmonyPatch(typeof(BigoniERO), "Start")]
    private static class SkipBigoniEROStart
    {
        private static bool Prefix(BigoniERO __instance)
        {
            if (!_brotherSceneActive)
            {
                return true;
            }

            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Skipping BigoniERO.Start (Game Over setup)");
            return false;
        }
    }

    [HarmonyPatch(typeof(StartBigoniERO), "place_GO")]
    private static class BlockPlaceGO
    {
        private static bool Prefix(StartBigoniERO __instance)
        {
            if (!IsBigoniBrother(__instance, out _))
            {
                return true;
            }

            MarkSceneActive(__instance);
            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Blocked place_GO()");
            return false;
        }
    }

    [HarmonyPatch(typeof(StartBigoniERO), "GOsceneLoad")]
    private static class BlockGOsceneLoad
    {
        private static bool Prefix(StartBigoniERO __instance)
        {
            if (!IsBigoniBrother(__instance, out _))
            {
                return true;
            }

            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Blocked GOsceneLoad()");
            return false;
        }
    }

    [HarmonyPatch(typeof(StartBigoniERO), "GO_PLACE")]
    private static class BlockGO_PLACE
    {
        private static bool Prefix(StartBigoniERO __instance)
        {
            if (!IsBigoniBrother(__instance, out _))
            {
                return true;
            }

            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Blocked GO_PLACE()");
            return false;
        }
    }

    [HarmonyPatch(typeof(StartBigoniERO), "fadeevent")]
    private static class BlockFadeEventInvoke
    {
        private static bool Prefix(StartBigoniERO __instance)
        {
            if (!IsBigoniBrother(__instance, out _))
            {
                return true;
            }

            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Blocked fadeevent()");
            return false;
        }
    }

    [HarmonyPatch(typeof(StartBigoniERO), "MAPrestart")]
    private static class BlockMAPrestart
    {
        private static bool Prefix(StartBigoniERO __instance)
        {
            if (!IsBigoniBrother(__instance, out _))
            {
                return true;
            }

            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Blocked MAPrestart()");
            return false;
        }
    }

    [HarmonyPatch(typeof(StartBigoniERO), "LoadSceneWait")]
    private static class BlockLoadSceneWait
    {
        private static bool Prefix(StartBigoniERO __instance, ref IEnumerator __result)
        {
            if (!IsBigoniBrother(__instance, out _))
            {
                return true;
            }

            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Blocked LoadSceneWait()");
            __result = EmptyCoroutine();
            return false;
        }

        private static IEnumerator EmptyCoroutine()
        {
            yield break;
        }
    }
}

