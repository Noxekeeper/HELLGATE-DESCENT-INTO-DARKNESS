using Rewired;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.UI;

/// <summary>
/// Keyboard edge detection compatible with Rewired (Unity Input alone misses keys in this game).
/// </summary>
internal static class EventCoreInput
{
    internal static bool GetKeyDown(KeyCode keyCode)
    {
        if (Input.GetKeyDown(keyCode))
            return true;

        Player player = ReInput.players.GetPlayer(0);
        if (player == null)
            return false;

        Keyboard keyboard = player.controllers.Keyboard;
        return keyboard != null && keyboard.GetKeyDown(keyCode);
    }
}
