using System;
using UnityEngine;
using System.Collections;
using NoREroMod.Patches.Enemy;
using NoREroMod.Patches.Enemy.Six_hand;
using NoREroMod.Patches.Enemy.Kakash;
using NoREroMod.Patches.Enemy.MafiaBossCustom;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod;

/// <summary>
/// MonoBehaviour for starting coroutines with delay handoff of GG
/// </summary>
public class DelayedHandoffScript : MonoBehaviour {
    /// <summary>
    /// Start coroutine with delay before handoff
    /// </summary>
    public void StartDelayedHandoff(object enemyInstance) {
        float delay = Plugin.handoffDelay.Value; // Use настройку from config
        string enemyType = enemyInstance?.GetType()?.Name ?? "Unknown";

        if (enemyType == "Mutudeero")
        {
            delay = 0f;
        }
        StartCoroutine(DelayedHandoffCoroutine(enemyInstance, delay));
    }
    
    /// <summary>
    /// Coroutine with delay before handoff
    /// </summary>
    private IEnumerator DelayedHandoffCoroutine(object enemyInstance, float delay) {
        // Plugin.Log.LogInfo($"[DELAYED HANDOFF] Задержка {delay} секунд before передачей...");
        
        // Wait указанное время (REAL TIME - not зависит from slow-mo!)
        yield return new WaitForSecondsRealtime(delay);
        
        // Plugin.Log.LogInfo($"[DELAYED HANDOFF] Задержка завершена, передаем ГГ!");
        
        // Определяем тип enemy и вызываем соответствующий метод передачи
        string enemyType = enemyInstance?.GetType()?.Name ?? "Unknown";
        // Plugin.Log.LogInfo($"[DELAYED HANDOFF] Enemy type: {enemyType}");
        
        // Вызываем функцию передачи depending on enemy type
        if (enemyType == "InquiBlackEro") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling InquisitionBlackPassPatch.ExecuteHandoff");
            InquisitionBlackPassPatch.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "InquisitionWhiteERO") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling InquisitionWhitePassPatch.ExecuteHandoff");
            InquisitionWhitePassPatch.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "InquisitionRedERO") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling InquisitionRedPassPatch.ExecuteHandoff");
            InquisitionRedPassPatch.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "VagrantMainERO") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling VagrantPassPatch.ExecuteHandoff");
            VagrantPassPatch.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "PrisonOfficerERO") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling PrisonOfficerPassPatch.ExecuteHandoff");
            PrisonOfficerPassPatch.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "LibrarianERO") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling LibrarianPassPatch.ExecuteHandoff");
            LibrarianPassPatch.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "MummyDogERO") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling MummyDogPassPatch.ExecuteHandoff");
            MummyDogPassPatch.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "PilgrimERO") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling PilgrimPassPatch.ExecuteHandoff");
            PilgrimPassPatch.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "MummyManERO") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling MummyManPassPatch.ExecuteHandoff");
            MummyManPassPatch.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "UndeadERO") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling UndeadPassPatch.ExecuteHandoff");
            UndeadPassPatch.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "CrowInquisitionERO" || enemyType == "GACrowInquisitionERO") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling CrowInquisitionPassLogic.ExecuteHandoff");
            NoREroMod.Patches.Enemy.CrowInquisition.CrowInquisitionPassLogic.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "EroTouzokuAXE") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling TouzokuAxePassPatch.ExecuteHandoff");
            TouzokuAxePassPatch.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "kakashi_ero2") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling KakasiPassLogic.ExecuteHandoff");
            KakasiPassLogic.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "SinnerslaveCrossbowERO") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling DoreiPassLogic.ExecuteHandoff");
            DoreiPassLogic.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "Mutudeero") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling MutudePassLogic.ExecuteHandoff");
            MutudePassLogic.ExecuteHandoff(enemyInstance);
        } else if (enemyType != null && enemyType.StartsWith("goblinero", StringComparison.OrdinalIgnoreCase)) {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling GoblinPassLogic.ExecuteHandoff");
            GoblinPassLogic.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "Bigoni" || enemyType == "BigoniBrother") {
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling BigoniBrotherPassLogic.ExecuteHandoff");
            BigoniBrotherPassLogic.ExecuteHandoff(enemyInstance);
        } else if (enemyType == "Mafiamuscle") {
            var mafia = enemyInstance as Mafiamuscle;
            if (mafia != null && mafia.gameObject != null && mafia.gameObject.name != null && mafia.gameObject.name.Contains(MafiaBossCustomStats.ObjectNameKey))
                MafiaBossCustomPassLogic.ExecuteHandoff(enemyInstance);
            else
                TouzokuNormalPassPatch.ExecuteHandoff(enemyInstance);
        } else {
            // For мечникоin (EroTouzoku) и остальных используем TouzokuNormalPassPatch
            // Plugin.Log.LogInfo("[DELAYED HANDOFF] Calling TouzokuNormalPassPatch.ExecuteHandoff");
            TouzokuNormalPassPatch.ExecuteHandoff(enemyInstance);
        }

        MindBrokenSystem.RegisterHandoff();
        MindBrokenUIPatch.RefreshLabel();
 
        // Удаляем скрипт after использования
        Destroy(this);
    }
}

