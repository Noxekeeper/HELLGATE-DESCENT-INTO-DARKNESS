using System;
using UnityEngine;
using DarkTonic.MasterAudio;

namespace NoREroMod.Systems.UI
{
    /// <summary>
    /// Менеджер sounds for меню настроек
    /// </summary>
    public static class SoundManager
    {
        private static AudioSource _audioSource;

        /// <summary>
        /// Инициализация soundsого менеджера
        /// </summary>
        public static void Initialize(GameObject canvas)
        {
            if (canvas != null && _audioSource == null)
            {
                _audioSource = canvas.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.volume = 0.5f;
            }
        }

        /// <summary>
        /// Воспроизвести sound кнопки
        /// </summary>
        public static void PlayButtonSound(ButtonType buttonType)
        {
            try
            {
                switch (buttonType)
                {
                    case ButtonType.Reset:
                        PlayResetSound();
                        break;
                    case ButtonType.ApplyClose:
                        PlayApplyCloseSound();
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[SoundManager] Failed to play {buttonType} sound: {ex.Message}");
            }
        }

        private static void PlayResetSound()
        {
            // Пытаемся использовать sound from QTE системы
            try
            {
                MasterAudio.PlaySound("snd_nuno", 1f, null, 0f, null, false, false);
            }
            catch
            {
                // Fallback on AudioSource with системным soundом
                PlaySystemSound("reset");
            }
        }

        private static void PlayApplyCloseSound()
        {
            // Пытаемся использовать sound победы
            try
            {
                MasterAudio.PlaySound("snd_Victory", 0.3f, null, 0f, null, false, false);
            }
            catch
            {
                // Fallback on AudioSource
                PlaySystemSound("applyclose");
            }
        }

        private static void PlaySystemSound(string soundType)
        {
            if (_audioSource == null) return;

            // Создаем простой soundsой сигнал через AudioSource
            // TODO: Загрузить аудио клипы from ресурсов
            _audioSource.PlayOneShot(null, 0.1f);
        }
    }

}
