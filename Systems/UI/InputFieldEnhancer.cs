using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NoREroMod.Systems.UI
{
    /// <summary>
    /// Улучшатель InputField for лучшits UX
    /// </summary>
    public class InputFieldEnhancer : MonoBehaviour, IPointerClickHandler
    {
        private InputField _inputField;
        private GameObject _caret;
        private float _lastClickTime;
        private bool _isDoubleClick;

        private void Awake()
        {
            _inputField = GetComponent<InputField>();
            if (_inputField == null)
            {
                Debug.LogError("[InputFieldEnhancer] No InputField component found!");
                return;
            }

            // Создаем каретку for лучшits отображения
            CreateCaret();

            // Настраиваем InputField for лучшits UX
            _inputField.caretBlinkRate = 1.5f;
            _inputField.caretWidth = 2;
            _inputField.selectionColor = new Color(0.7f, 0.9f, 1f, 0.5f); // Светло-голубая подсветка выделения
        }

        private void CreateCaret()
        {
            if (_caret != null) return;

            // Создаем каретку as дочерний объект InputField
            _caret = new GameObject("CustomCaret");
            _caret.transform.SetParent(transform, false);

            var caretImage = _caret.AddComponent<Image>();
            caretImage.color = Color.white;
            caretImage.raycastTarget = false;

            var caretRect = _caret.GetComponent<RectTransform>();
            caretRect.sizeDelta = new Vector2(2, _inputField.textComponent.fontSize * 1.2f);
            caretRect.anchorMin = new Vector2(0, 0.5f);
            caretRect.anchorMax = new Vector2(0, 0.5f);
            caretRect.pivot = new Vector2(0, 0.5f);

            _caret.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_inputField == null) return;

            // Проверяем двойной клик for выделения всits text
            float timeSinceLastClick = Time.time - _lastClickTime;
            if (timeSinceLastClick < 0.3f)
            {
                _isDoubleClick = true;
                _inputField.Select();
                StartCoroutine(SelectAllTextDelayed());
            }
            else
            {
                _isDoubleClick = false;
            }

            _lastClickTime = Time.time;
        }

        private System.Collections.IEnumerator SelectAllTextDelayed()
        {
            yield return null; // Wait следующий кадр
            _inputField.MoveTextStart(false);
            _inputField.MoveTextEnd(true);
        }

        public void ShowCaretAtPosition(int position)
        {
            if (_caret == null || _inputField == null || _inputField.textComponent == null) return;

            string text = _inputField.text;
            if (position > text.Length) position = text.Length;

            // Calculate position каретки
            string textBeforeCaret = text.Substring(0, position);
            float caretX = CalculateTextWidth(textBeforeCaret, _inputField.textComponent);

            var caretRect = _caret.GetComponent<RectTransform>();
            caretRect.anchoredPosition = new Vector2(caretX, 0);
            _caret.SetActive(true);

            // Автоматически скрываем каретку через время
            CancelInvoke("HideCaret");
            Invoke("HideCaret", 2f);
        }

        private void HideCaret()
        {
            if (_caret != null)
            {
                _caret.SetActive(false);
            }
        }

        private float CalculateTextWidth(string text, Text textComponent)
        {
            if (textComponent == null) return 0;

            // Простой calculation text width
            return text.Length * textComponent.fontSize * 0.6f;
        }

        private void OnDestroy()
        {
            if (_caret != null)
            {
                Destroy(_caret);
            }
        }
    }

    /// <summary>
    /// Расширенный InputField with improved UX
    /// </summary>
    public static class InputFieldExtensions
    {
        /// <summary>
        /// Добавить улучшения к InputField
        /// </summary>
        public static void Enhance(this InputField inputField)
        {
            if (inputField.GetComponent<InputFieldEnhancer>() == null)
            {
                inputField.gameObject.AddComponent<InputFieldEnhancer>();
            }
        }
    }
}
