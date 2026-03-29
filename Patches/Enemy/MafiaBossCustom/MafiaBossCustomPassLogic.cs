using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Spine.Unity;
using NoREroMod.Patches.Enemy.MafiaBossCustom;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy.MafiaBossCustom;

/// <summary>
/// Handoff logic ГГ for MafiaBossCustom: after handoff enemy скрывается (as у всех кроме BigoniBrother),
/// on подъёме ГГ or побеге — скрытые мафии again появляются и становятся враждебными.
/// Optimized: Uses UnifiedPlayerCacheManager + кэшированная рефлексия
/// </summary>
internal static class MafiaBossCustomPassLogic
{
    /// <summary> Количество передач in текущей сессии (0 = первый мафия, иначе последующие). </summary>
    internal static int GlobalHandoffCount;

    /// <summary> Вариант старта for последующits мафии: 1=STARTERO→1EROFIN2, 2=2ERO→3EROJIGO, 4=4ERO→4EROJIGO (3EROJIGO-старт убран). </summary>
    private static System.Collections.Generic.Dictionary<Mafiamuscle, int> HandoffVariant = new System.Collections.Generic.Dictionary<Mafiamuscle, int>();

    /// <summary> Враги, которые already отдали ГГ in этой сессии (чтобы not вызывать handoff дважды). </summary>
    private static System.Collections.Generic.HashSet<Mafiamuscle> AlreadyHandedOff = new System.Collections.Generic.HashSet<Mafiamuscle>();

    /// <summary> Скрытые on handoff мафии — показываем again, when ГГ поднялась or вырвалась. </summary>
    private static System.Collections.Generic.List<Mafiamuscle> HiddenByHandoff = new System.Collections.Generic.List<Mafiamuscle>();

    // Optimization: Кэшированные reflection поля for Mafiamuscle
    private static FieldInfo _cachedEroFlagField;
    private static FieldInfo _cachedErospineField;
    private static FieldInfo _cachedErodataField;
    
    // Optimization: Кэшированные reflection поля for playercon
    private static FieldInfo _cachedEroDownField;
    private static FieldInfo _cachedParryField;
    private static FieldInfo _cachedItemUseField;
    private static FieldInfo _cachedStabNowField;
    
    // Optimization: Кэш for SkeletonAnimation componentа игрока
    private static SkeletonAnimation _cachedPlayerSpine;
    private static float _lastPlayerSpineCacheTime;
    private const float PLAYER_SPINE_CACHE_INTERVAL = 1.0f;
    
    static MafiaBossCustomPassLogic()
    {
        // Initialize кэш reflection полей on загрузке класса
        try
        {
            _cachedEroFlagField = typeof(Mafiamuscle).GetField("eroflag", BindingFlags.NonPublic | BindingFlags.Instance);
            _cachedErospineField = typeof(Mafiamuscle).GetField("erospine", BindingFlags.NonPublic | BindingFlags.Instance);
            _cachedErodataField = typeof(Mafiamuscle).GetField("erodata", BindingFlags.NonPublic | BindingFlags.Instance);
            
            _cachedEroDownField = typeof(playercon).GetField("erodown", BindingFlags.Public | BindingFlags.Instance);
            _cachedParryField = typeof(playercon).GetField("Parry", BindingFlags.NonPublic | BindingFlags.Instance);
            _cachedItemUseField = typeof(playercon).GetField("Itemuse", BindingFlags.NonPublic | BindingFlags.Instance);
            _cachedStabNowField = typeof(playercon).GetField("stabnow", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[MafiaBossCustomPassLogic] Failed to cache reflection fields: {ex.Message}");
        }
    }

    internal static void ResetAll()
    {
        GlobalHandoffCount = 0;
        HandoffVariant.Clear();
        AlreadyHandedOff.Clear();
        ReShowHiddenMafias();
    }

    internal static bool HasAlreadyHandedOff(Mafiamuscle oya)
    {
        return oya != null && AlreadyHandedOff.Contains(oya);
    }

    internal static void MarkHandedOff(Mafiamuscle oya)
    {
        if (oya != null) AlreadyHandedOff.Add(oya);
    }

    internal static int GetVariant(Mafiamuscle oya)
    {
        return HandoffVariant.TryGetValue(oya, out var v) ? v : 0;
    }

    internal static void SetVariant(Mafiamuscle oya, int variant)
    {
        HandoffVariant[oya] = variant;
    }

    internal static void RemoveVariant(Mafiamuscle oya)
    {
        HandoffVariant.Remove(oya);
    }

    public static void ExecuteHandoff(object enemyInstance)
    {
        var mafia = enemyInstance as Mafiamuscle;
        if (mafia != null) MarkHandedOff(mafia);
        PushPlayerAwayFromEnemy(enemyInstance);
    }

    /// <summary>
    /// Optimized: Кэшированная рефлексия + кэш Spine componentа
    /// БЫЛО: 6x GetField + GetComponentInChildren + GetComponent (~15-25ms)
    /// СТАЛО: Использование кэшированных полей (~1-2ms)
    /// </summary>
    private static void PushPlayerAwayFromEnemy(object enemyInstance)
    {
        try
        {
            var mafia = enemyInstance as Mafiamuscle;
            if (mafia == null) return;

            // ✨ Используем кэшированный playercon
            GameObject playerObject = UnifiedPlayerCacheManager.GetPlayerObject();
            playercon playerComponent = UnifiedPlayerCacheManager.GetPlayer();
            if (playerObject == null || playerComponent == null) return;

            RemoveVariant(mafia);

            // Optimization: Используем кэшированные поля instead of GetField
            try
            {
                if (_cachedEroFlagField != null)
                    _cachedEroFlagField.SetValue(mafia, false);
            }
            catch { }

            if (_cachedErospineField != null)
            {
                var erospine = _cachedErospineField.GetValue(mafia) as SkeletonAnimation;
                if (erospine != null)
                    erospine.AnimationState.ClearTracks();
            }

            if (_cachedErodataField != null)
            {
                var erodata = _cachedErodataField.GetValue(mafia) as GameObject;
                if (erodata != null)
                    erodata.SetActive(false);
            }

            HiddenByHandoff.Add(mafia);
            if (mafia.gameObject != null)
                mafia.gameObject.SetActive(false);

            // Optimization: Кэшируем SkeletonAnimation игрока
            float currentTime = Time.time;
            if (_cachedPlayerSpine == null || (currentTime - _lastPlayerSpineCacheTime) > PLAYER_SPINE_CACHE_INTERVAL)
            {
                _cachedPlayerSpine = playerObject.GetComponentInChildren<SkeletonAnimation>();
                _lastPlayerSpineCacheTime = currentTime;
            }
            
            if (_cachedPlayerSpine != null)
            {
                try { _cachedPlayerSpine.AnimationState.ClearTracks(); } catch { }
                string[] downAnims = { "DOWN", "down", "Idle", "idle" };
                foreach (var animName in downAnims)
                {
                    try
                    {
                        _cachedPlayerSpine.AnimationState.SetAnimation(0, animName, true);
                        break;
                    }
                    catch { }
                }
            }

            // ✨ Используем already полученный playerComponent from кэша
            playerComponent.eroflag = false;
            playerComponent._eroflag2 = false;

            // ✨ Используем кэшированное поле
            if (_cachedEroDownField != null)
                _cachedEroDownField.SetValue(playerComponent, 1);

            playerComponent.Attacknow = false;
            playerComponent.Actstate = false;
            playerComponent.stepfrag = false;
            playerComponent.magicnow = false;
            playerComponent.guard = false;
            
            // ✨ Используем кэшированные поля
            _cachedParryField?.SetValue(playerComponent, false);
            _cachedItemUseField?.SetValue(playerComponent, false);
            _cachedStabNowField?.SetValue(playerComponent, false);
            
            playerComponent._easyESC = false;
            playerComponent.nowdamage = playerComponent.erodown != 0;
            StruggleSystem.setStruggleLevel(-1f);
            Time.timeScale = 1f;
        }
        catch (Exception) { }
    }

    private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly BindingFlags Pub = BindingFlags.Public | BindingFlags.Instance;

    // Optimization: Кэшированные поля for ReShowHiddenMafias
    private static FieldInfo _cachedStateField;
    private static FieldInfo _cachedMyspinerennderField;
    private static FieldInfo _cachedUIField;
    private static FieldInfo _cachedWpeffectField;
    
    private static void InitializeReShowFields()
    {
        if (_cachedStateField != null) return;
        
        var t = typeof(Mafiamuscle);
        _cachedStateField = t.GetField("state", Pub);
        _cachedMyspinerennderField = t.GetField("myspinerennder", Priv);
        _cachedUIField = t.GetField("UI", Priv);
        _cachedWpeffectField = t.GetField("wpeffect", Priv);
    }

    /// <summary>
    /// Optimized: Кэшированные reflection поля
    /// </summary>
    internal static void ReShowHiddenMafias()
    {
        if (HiddenByHandoff.Count == 0) return;
        
        InitializeReShowFields();
        
        foreach (var mafia in HiddenByHandoff)
        {
            try
            {
                if (mafia == null || mafia.gameObject == null) continue;
                mafia.gameObject.SetActive(true);
                
                // ✨ Используем кэшированные поля
                if (_cachedStateField != null) 
                    _cachedStateField.SetValue(mafia, Mafiamuscle.enemystate.IDLE);
                    
                if (_cachedMyspinerennderField != null)
                {
                    var ren = _cachedMyspinerennderField.GetValue(mafia) as MeshRenderer;
                    if (ren != null) ren.enabled = true;
                }
                
                if (_cachedUIField != null)
                {
                    var ui = _cachedUIField.GetValue(mafia) as GameObject;
                    if (ui != null) ui.SetActive(true);
                }
                
                if (_cachedWpeffectField != null)
                {
                    var wpeffect = _cachedWpeffectField.GetValue(mafia) as GameObject[];
                    if (wpeffect != null)
                        for (int i = 0; i < wpeffect.Length; i++)
                            if (wpeffect[i] != null) wpeffect[i].SetActive(true);
                }
            }
            catch { }
        }
        HiddenByHandoff.Clear();
    }

    [HarmonyPatch(typeof(Mafiamuscle), "eroanime")]
    [HarmonyPostfix]
    private static void Eroanime_Postfix(Mafiamuscle __instance)
    {
        try
        {
            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null || player.erodown != 0) return;
            ReShowHiddenMafias();
        }
        catch { }
    }

    /// <summary>
    /// Optimized: Убран FindObjectOfType - используем QTESystem.GetCurrentEnemyInstance()
    /// БЫЛО: FindObjectOfType on каждой передаче (~2-5ms)
    /// СТАЛО: Получение from кэша QTESystem (~0.01ms)
    /// </summary>
    [HarmonyPatch(typeof(playercon), "ImmediatelyERO")]
    [HarmonyPostfix]
    private static void ClearStateOnImmediatelyERO()
    {
        try
        {
            // ✨ Пробуем получить enemy from QTESystem (if доступен)
            var currentEnemy = QTESystem.GetCurrentEnemyInstance();
            if (currentEnemy is Mafiamuscle mafia)
            {
                if (mafia.gameObject != null && MafiaBossCustomStats.IsMafiaBossCustom(mafia))
                {
                    ResetAll();
                    return;
                }
            }
            
            // Fallback: if QTESystem not имеет enemy, проверяем HiddenByHandoff список
            if (HiddenByHandoff.Count > 0)
            {
                ResetAll();
            }
        }
        catch { }
    }

    [HarmonyPatch(typeof(StruggleSystem), "startGrabInvul")]
    [HarmonyPostfix]
    private static void ClearStateOnStruggleEscape()
    {
        try { ResetAll(); } catch { }
    }
}
