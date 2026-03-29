using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoREroMod.Systems.UI
{
    /// <summary>
    /// Settings value manager - apply, reset, update
    /// </summary>
    public static class SettingsValueManager
    {
        /// <summary>
        /// Update values of all settings in UI from ConfigEntry
        /// </summary>
        public static void RefreshSettingsValues()
        {
            Plugin.Log?.LogInfo("[SettingsValueManager] Refreshing all settings values...");

            var settingsElements = UISettingsBuilder.GetSettingsElements();

            foreach (var kvp in settingsElements)
            {
                string labelText = kvp.Key;
                var element = kvp.Value;

                try
                {
                    if (element.inputField == null)
                    {
                        Plugin.Log?.LogWarning($"[SettingsValueManager] Skipping '{labelText}' - inputField is null");
                        continue;
                    }

                    if (element.configEntry != null)
                    {
                        float newValue = element.configEntry.Value;
                        // Update InputField
                        element.inputField.text = newValue.ToString($"F{element.decimals}");
                        // Sync originalValue with ConfigEntry
                        element.originalValue = newValue;
                        // Reset flag changes
                        element.isChanged = false;
                        // Remove border
                        if (element.borderImage != null)
                        {
                            element.borderImage.color = new Color(1f, 1f, 1f, 0f);
                        }
                        Plugin.Log?.LogInfo($"[SettingsValueManager] Refreshed '{labelText}' to {newValue}");
                    }
                    else if (element.configEntryInt != null)
                    {
                        int newValue = element.configEntryInt.Value;
                        // Update InputField
                        element.inputField.text = newValue.ToString();
                        // Sync originalValue with ConfigEntry
                        element.originalValue = newValue;
                        // Reset flag changes
                        element.isChanged = false;
                        // Remove border
                        if (element.borderImage != null)
                        {
                            element.borderImage.color = new Color(1f, 1f, 1f, 0f);
                        }
                        Plugin.Log?.LogInfo($"[SettingsValueManager] Refreshed '{labelText}' to {newValue}");
                    }
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.LogError($"[SettingsValueManager] Error refreshing '{labelText}': {ex.Message}");
                }
            }

            var qteToggle = UISettingsBuilder.GetQTESystemToggle();
            if (qteToggle != null && Plugin.enableQTESystem != null)
            {
                qteToggle.isOn = Plugin.enableQTESystem.Value;
            }
        }

        /// <summary>
        /// Apply settings (save to ConfigEntry)
        /// </summary>
        public static void ApplySettings()
        {
            Plugin.Log?.LogInfo("[SettingsValueManager] Applying settings...");

            List<GameObject> changedContainers = new List<GameObject>();
            var settingsElements = UISettingsBuilder.GetSettingsElements();

            foreach (var kvp in settingsElements)
            {
                string labelText = kvp.Key;
                var element = kvp.Value;

                try
                {
                    if (element.inputField == null)
                    {
                        continue;
                    }

                    // Get current значение from InputField и compare with originalValue
                    // Apply changes if they exist (even if isChanged not set)
                    if (element.configEntry != null)
                    {
                        // Get value from InputField
                        string text = element.inputField.text;
                        if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float currentValue))
                        {
                            currentValue = Mathf.Clamp(currentValue, element.minValue, element.maxValue);

                            // Check if changed значение vs originalValue
                            bool hasChanged = Mathf.Abs(currentValue - element.originalValue) > 0.0001f;

                            Plugin.Log?.LogInfo($"[SettingsValueManager] Apply check for '{labelText}': currentValue={currentValue}, originalValue={element.originalValue}, hasChanged={hasChanged}, isChanged={element.isChanged}");

                            if (hasChanged || element.isChanged)
                            {
                                // IMPORTANT: Save to ConfigEntry BEFORE updating originalValue
                                element.configEntry.Value = currentValue;

                                // Update originalValue AFTER saving in ConfigEntry
                                element.originalValue = currentValue;

                                // Update text in InputField with correct formatting
                                string formattedValue = currentValue.ToString($"F{element.decimals}");
                                element.inputField.text = formattedValue;
                                if (element.inputField.textComponent != null)
                                {
                                    element.inputField.textComponent.text = formattedValue;
                                }

                                // Скрываем border
                                if (element.borderImage != null)
                                {
                                    element.borderImage.color = new Color(1f, 1f, 1f, 0f);
                                }

                                element.isChanged = false;

                                // Add container for animation
                                if (element.container != null)
                                {
                                    changedContainers.Add(element.container);
                                }
                            }
                        }
                    }
                    else if (element.configEntryInt != null)
                    {
                        string text = element.inputField.text;
                        if (int.TryParse(text, out int currentValue))
                        {
                            currentValue = Mathf.Clamp(currentValue, (int)element.minValue, (int)element.maxValue);

                            // Check if changed значение vs originalValue
                            bool hasChanged = Mathf.Abs(currentValue - element.originalValue) > 0.5f;

                            Plugin.Log?.LogInfo($"[SettingsValueManager] Apply check for '{labelText}': currentValue={currentValue}, originalValue={element.originalValue}, hasChanged={hasChanged}, isChanged={element.isChanged}");

                            if (hasChanged || element.isChanged)
                            {
                                // IMPORTANT: Save to ConfigEntry BEFORE updating originalValue
                                element.configEntryInt.Value = currentValue;

                                // Update originalValue AFTER saving in ConfigEntry
                                element.originalValue = currentValue;

                                // Update text in InputField
                                string formattedValue = currentValue.ToString();
                                element.inputField.text = formattedValue;
                                if (element.inputField.textComponent != null)
                                {
                                    element.inputField.textComponent.text = formattedValue;
                                }

                                // Скрываем border
                                if (element.borderImage != null)
                                {
                                    element.borderImage.color = new Color(1f, 1f, 1f, 0f);
                                }

                                element.isChanged = false;

                                // Add container for animation
                                if (element.container != null)
                                {
                                    changedContainers.Add(element.container);
                                }

                                Plugin.Log?.LogInfo($"[SettingsValueManager] ✅ Applied '{labelText}': {currentValue}");
                            }
                        }
                    }
                    else
                    {
                        // If значение not изменилось, синхронизируем originalValue with ConfigEntry
                        // This needed for случаев, when ConfigEntry was changed externally
                        if (element.configEntry != null)
                        {
                            element.originalValue = element.configEntry.Value;
                        }
                        else if (element.configEntryInt != null)
                        {
                            element.originalValue = element.configEntryInt.Value;
                        }

                        // Ensure that border hidden
                        if (element.borderImage != null)
                        {
                            element.borderImage.color = new Color(1f, 1f, 1f, 0f);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.LogError($"[SettingsValueManager] Error applying '{labelText}': {ex.Message}");
                }
            }

            // Show animation for changed sections
            if (changedContainers.Count > 0)
            {
                AnimationManager.StartFlashAnimation(changedContainers);
            }

            Plugin.Log?.LogInfo("[SettingsValueManager] ✅ Settings applied successfully");
        }

        /// <summary>
        /// Reset settings to default values
        /// </summary>
        public static void ResetToDefaults()
        {
            Plugin.Log?.LogInfo("[SettingsValueManager] Resetting all settings to defaults...");

            // QTE System Toggle
            if (Plugin.enableQTESystem != null)
            {
                Plugin.enableQTESystem.Value = true;
                Plugin.Log?.LogInfo("[SettingsValueManager] Reset enableQTESystem to true");
            }

            // SP Gain
            if (Plugin.qteSPGainBase != null) { Plugin.qteSPGainBase.Value = 0.05f; Plugin.Log?.LogInfo("Reset qteSPGainBase to 0.05f"); }
            if (Plugin.qteSPGainMin != null) { Plugin.qteSPGainMin.Value = 0.02f; Plugin.Log?.LogInfo("Reset qteSPGainMin to 0.02f"); }
            if (Plugin.qteYellowButtonSPGainMin != null) { Plugin.qteYellowButtonSPGainMin.Value = 0.15f; Plugin.Log?.LogInfo("Reset qteYellowButtonSPGainMin to 0.15f"); }
            if (Plugin.qteYellowButtonSPGainMax != null) { Plugin.qteYellowButtonSPGainMax.Value = 0.3f; Plugin.Log?.LogInfo("Reset qteYellowButtonSPGainMax to 0.3f"); }

            // Penalties
            if (Plugin.qteMPPenaltyPercent != null) { Plugin.qteMPPenaltyPercent.Value = 0.3f; Plugin.Log?.LogInfo("Reset qteMPPenaltyPercent to 0.3f"); }
            if (Plugin.qteMindBrokenPenaltyPercent != null) { Plugin.qteMindBrokenPenaltyPercent.Value = 0.01f; Plugin.Log?.LogInfo("Reset qteMindBrokenPenaltyPercent to 0.01f"); }
            if (Plugin.qteRedButtonMindBrokenPenalty != null) { Plugin.qteRedButtonMindBrokenPenalty.Value = 0.02f; Plugin.Log?.LogInfo("Reset qteRedButtonMindBrokenPenalty to 0.02f"); }
            if (Plugin.qteSPPenaltyMultiplier != null) { Plugin.qteSPPenaltyMultiplier.Value = 2.0f; Plugin.Log?.LogInfo("Reset qteSPPenaltyMultiplier to 2.0f"); }

            // Timers
            if (Plugin.qteWindowDurationMin != null) { Plugin.qteWindowDurationMin.Value = 2f; Plugin.Log?.LogInfo("Reset qteWindowDurationMin to 2f"); }
            if (Plugin.qteWindowDurationMax != null) { Plugin.qteWindowDurationMax.Value = 3.5f; Plugin.Log?.LogInfo("Reset qteWindowDurationMax to 3.5f"); }
            if (Plugin.qteCooldownDurationMin != null) { Plugin.qteCooldownDurationMin.Value = 2f; Plugin.Log?.LogInfo("Reset qteCooldownDurationMin to 2f"); }
            if (Plugin.qteCooldownDurationMax != null) { Plugin.qteCooldownDurationMax.Value = 4f; Plugin.Log?.LogInfo("Reset qteCooldownDurationMax to 4f"); }

            // Visual Effects
            if (Plugin.qteRotationSpeed != null) { Plugin.qteRotationSpeed.Value = 90f; Plugin.Log?.LogInfo("Reset qteRotationSpeed to 90f"); }
            if (Plugin.qteRotationStartTime != null) { Plugin.qteRotationStartTime.Value = 0.5f; Plugin.Log?.LogInfo("Reset qteRotationStartTime to 0.5f"); }
            if (Plugin.qteColorChangeInterval != null) { Plugin.qteColorChangeInterval.Value = 1f; Plugin.Log?.LogInfo("Reset qteColorChangeInterval to 1f"); }
            if (Plugin.qtePressIndicatorDuration != null) { Plugin.qtePressIndicatorDuration.Value = 0.15f; Plugin.Log?.LogInfo("Reset qtePressIndicatorDuration to 0.15f"); }
            if (Plugin.qteMaxButtonTransparency != null) { Plugin.qteMaxButtonTransparency.Value = 0.5f; Plugin.Log?.LogInfo("Reset qteMaxButtonTransparency to 0.5f"); }
            if (Plugin.qteMaxPinkShadowIntensity != null) { Plugin.qteMaxPinkShadowIntensity.Value = 1f; Plugin.Log?.LogInfo("Reset qteMaxPinkShadowIntensity to 1f"); }

            // Sound
            if (Plugin.qteSuccessVolumeMultiplier != null) { Plugin.qteSuccessVolumeMultiplier.Value = 1.0f; Plugin.Log?.LogInfo("Reset qteSuccessVolumeMultiplier to 1.0f"); }
            if (Plugin.qteFailureVolumeMultiplier != null) { Plugin.qteFailureVolumeMultiplier.Value = 1.0f; Plugin.Log?.LogInfo("Reset qteFailureVolumeMultiplier to 1.0f"); }

            // Combo
            if (Plugin.qteComboMilestone != null) { Plugin.qteComboMilestone.Value = 10; Plugin.Log?.LogInfo("Reset qteComboMilestone to 10"); }

            Plugin.Log?.LogInfo("[SettingsValueManager] Settings reset to defaults");

            // Update UI
            RefreshSettingsValues();
        }
    }
}
