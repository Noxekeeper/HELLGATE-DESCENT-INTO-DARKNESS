using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Сбрасывает Time.timeScale in 1f on выходе from захвата.
/// Враги ставят timeScale = 0.2f on старте H-scene и планируют Invoke on своём GameObject.
/// On быстром побеге ERO-объект деактивируется и Invoke not выполняется — время остаётся 0.2.
/// Патч ловит переход eroflag true→false и force восстанавливает timeScale.
/// </summary>
internal static class TimeScaleResetOnEscapePatch
{
    private static bool _wasInGrabLastFrame;

    /// <summary>Вызывается from PlayerConUpdateDispatcher</summary>
    internal static void Process(bool eroflag)
    {
        try
        {
            bool isInGrab = eroflag;
            if (_wasInGrabLastFrame && !isInGrab)
            {
                if (Time.timeScale != 1f && Time.timeScale != 0f)
                {
                    Time.timeScale = 1f;
                }
            }
            _wasInGrabLastFrame = isInGrab;
        }
        catch
        {
            _wasInGrabLastFrame = false;
        }
    }
}
