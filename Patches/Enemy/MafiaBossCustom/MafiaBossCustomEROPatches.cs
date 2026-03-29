using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using DarkTonic.MasterAudio;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Cache;
using Spine.Unity;

namespace NoREroMod.Patches.Enemy.MafiaBossCustom;

/// <summary>
/// Патчи EroMafiamuscle: for MafiaBossCustom (by name oya) блокируем BadEnd on 4ERO и делаем передачу on 1EROFIN2 / 3EROJIGO / 4EROJIGO.
/// On первом событии in ERO, if currentAnim==START и globalHandoffCount>0 — подменяем on STARTERO/2ERO/4ERO (3EROJIGO убран — глючит).
/// Optimized: Uses UnifiedPlayerCacheManager + кэшированная рефлексия
/// </summary>
internal static class MafiaBossCustomEROPatches
{
    private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
    
    // Critical optimization: Кэшируем все FieldInfo (вызываются on каждом Spine событии!)
    private static FieldInfo _cachedOyaField;
    private static FieldInfo _cachedMyspineField;
    private static FieldInfo _cachedAnycanvasobjField;
    private static FieldInfo _cachedFlagFieldPublic;
    private static FieldInfo _cachedFlagFieldPrivate;
    private static FieldInfo _cachedPlayerField;
    
    static MafiaBossCustomEROPatches()
    {
        try
        {
            var eroType = typeof(EroMafiamuscle);
            _cachedOyaField = eroType.GetField("oya", Priv);
            _cachedMyspineField = eroType.GetField("myspine", Priv);
            _cachedAnycanvasobjField = eroType.GetField("anycanvasobj", Priv);
            _cachedFlagFieldPublic = eroType.GetField("Flag", BindingFlags.Public | BindingFlags.Instance);
            _cachedFlagFieldPrivate = eroType.GetField("Flag", Priv);
            _cachedPlayerField = eroType.GetField("player", Priv);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[MafiaBossCustomEROPatches] Failed to cache FieldInfo: {ex.Message}");
        }
    }

    private static string GetEventName(Spine.Event e)
    {
        if (e?.Data == null) return null;
        var prop = e.Data.GetType().GetProperty("Name") ?? e.Data.GetType().GetProperty("name");
        return prop?.GetValue(e.Data, null) as string;
    }

    private static Mafiamuscle GetOya(EroMafiamuscle ero)
    {
        return _cachedOyaField?.GetValue(ero) as Mafiamuscle;
    }

    private static SkeletonAnimation GetMyspine(EroMafiamuscle ero)
    {
        return _cachedMyspineField?.GetValue(ero) as SkeletonAnimation;
    }

    private static void StartDelayedHandoff(Mafiamuscle oya)
    {
        // Optimization: use cached playercon
        var playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
        if (playerObj != null)
        {
            var script = playerObj.GetComponent<DelayedHandoffScript>() ?? playerObj.AddComponent<DelayedHandoffScript>();
            script.StartDelayedHandoff(oya);
        }
        else
        {
            var temp = new GameObject("DelayedHandoffTemp");
            var script = temp.AddComponent<DelayedHandoffScript>();
            script.StartDelayedHandoff(oya);
        }
    }

    /// <summary>
    /// Блокируем BadEnd: for MafiaBossCustom fadeevent() not выполняется.
    /// </summary>
    [HarmonyPatch(typeof(EroMafiamuscle), "fadeevent")]
    [HarmonyPrefix]
    private static bool Fadeevent_Prefix(EroMafiamuscle __instance)
    {
        var oya = GetOya(__instance);
        if (oya != null && MafiaBossCustomStats.IsMafiaBossCustom(oya))
            return false;
        return true;
    }

    /// <summary>
    /// Postfix on OnEvent: При 1EROFIN2 / 3EROJIGO / 4EROJIGO запускаем handoff by варианту.
    /// 
    /// ✨ ВАЖНО: Подмеon START on STARTERO/2ERO/4ERO теперь происходит in OnTriggerStay2D,
    /// so that избежать двойной работы (START начинает проигрываться → прерывается → новая анимация)
    /// </summary>
    [HarmonyPatch(typeof(EroMafiamuscle), "OnEvent")]
    [HarmonyPostfix]
    private static void OnEvent_Postfix(EroMafiamuscle __instance, Spine.Event e)
    {
        try
        {
            var oya = GetOya(__instance);
            if (oya == null || !MafiaBossCustomStats.IsMafiaBossCustom(oya))
                return;

            string name = GetEventName(e);
            if (string.IsNullOrEmpty(name)) return;

            if (MafiaBossCustomPassLogic.HasAlreadyHandedOff(oya))
                return;

            int variantEv = MafiaBossCustomPassLogic.GetVariant(oya);

            if (name == "1EROFIN2")
            {
                if (variantEv == 0 || variantEv == 1)
                {
                    MafiaBossCustomPassLogic.GlobalHandoffCount++;
                    EnemyHandoffSystem.GlobalHandoffCount++;
                    StartDelayedHandoff(oya);
                }
                return;
            }

            if (name == "3EROJIGO")
            {
                if (variantEv == 2)
                {
                    MafiaBossCustomPassLogic.GlobalHandoffCount++;
                    EnemyHandoffSystem.GlobalHandoffCount++;
                    StartDelayedHandoff(oya);
                }
                return;
            }

            if (name == "4EROJIGO")
            {
                if (variantEv == 4)
                {
                    MafiaBossCustomPassLogic.GlobalHandoffCount++;
                    EnemyHandoffSystem.GlobalHandoffCount++;
                    StartDelayedHandoff(oya);
                }
            }

            // Убираем канваwith BadEnd «Press any button» и флаг — for MafiaBossCustom они not нужны (handoff instead of бэдэнда)
            ClearBadEndCanvasAndFlag(__instance);
        }
        catch (Exception) { }
    }

    private static void ClearBadEndCanvasAndFlag(EroMafiamuscle ero)
    {
        try
        {
            // ✨ Используем кэшированные FieldInfo
            if (_cachedAnycanvasobjField != null)
            {
                var obj = _cachedAnycanvasobjField.GetValue(ero) as GameObject;
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                    _cachedAnycanvasobjField.SetValue(ero, null);
                }
            }
            
            var flagField = _cachedFlagFieldPublic ?? _cachedFlagFieldPrivate;
            if (flagField != null)
                flagField.SetValue(ero, false);
            
            if (_cachedPlayerField != null && _cachedPlayerField.GetValue(ero) is playercon pc)
                pc._easyESC = false;
        }
        catch { }
    }
}
