using UnityEngine;
using NoREroMod;
using NoREroMod.Systems.EventCore.Core;

namespace NoREroMod.Systems.EventCore.UI;

/// <summary>
/// Reads keyboard input while an EventCore session is active. Attached to the plugin behaviour object.
/// </summary>
internal sealed class EventCoreModalDriver : MonoBehaviour
{
    private void Update()
    {
        KeyCode dev = Plugin.eventCoreDevHotkey.Value;
        if (dev != KeyCode.None && EventCoreInput.GetKeyDown(dev))
        {
            if (!Plugin.eventCoreEnable.Value)
            {
                Plugin.Log?.LogWarning(
                    "[EventCore] Dev hotkey ignored: set EventCore → Enable = true in BepInEx/config/NoREroMod_HellGate.cfg (then restart if you just changed it).");
                return;
            }

            EventCoreRuntime.TryBeginSession(Plugin.eventCoreDevEventId.Value);
        }

        if (!EventCoreRuntime.IsSessionOpen || !EventCoreModalCanvas.IsVisible)
            return;

        if (EventCoreRuntime.ContinuePromptActive)
        {
            if (EventCoreInput.GetKeyDown(KeyCode.Alpha1) ||
                EventCoreInput.GetKeyDown(KeyCode.E) ||
                EventCoreInput.GetKeyDown(KeyCode.Return) ||
                EventCoreInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                EventCoreRuntime.AdvanceContinuePrompt();
            }

            return;
        }

        if (EventCoreInput.GetKeyDown(KeyCode.Alpha1) && EventCoreRuntime.CanActivateBrokerPaySlot(0))
            EventCoreRuntime.AdvanceChoiceStep(0);
        else if (EventCoreInput.GetKeyDown(KeyCode.Alpha2) && EventCoreRuntime.CanActivateBrokerPaySlot(1))
            EventCoreRuntime.AdvanceChoiceStep(1);
        else if (EventCoreInput.GetKeyDown(KeyCode.Alpha3) && EventCoreRuntime.CanActivateBrokerPaySlot(2))
            EventCoreRuntime.AdvanceChoiceStep(2);
        else if (EventCoreInput.GetKeyDown(KeyCode.Alpha4) && EventCoreRuntime.CanActivateBrokerPaySlot(3))
            EventCoreRuntime.AdvanceChoiceStep(3);
        else if (EventCoreInput.GetKeyDown(KeyCode.Alpha5) && EventCoreRuntime.CanActivateBrokerPaySlot(4))
            EventCoreRuntime.AdvanceChoiceStep(4);
    }
}
