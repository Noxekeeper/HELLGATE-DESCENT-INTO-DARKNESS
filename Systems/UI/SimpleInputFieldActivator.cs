using System;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.UI
{
    /// <summary>
    /// Simple activator InputField with improved UX for Unity 5.6
    /// Provides activation InputField on click and input handling from keyboard
    /// </summary>
    public class SimpleInputFieldActivator : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IDragHandler
    {
        public InputField inputField;
        private ScrollRect _scrollRect;
        private bool _isActivated = false;
        private GameObject _caretObj = null; // Visual caret
        private UnityEngine.Coroutine _caretBlinkCoroutine = null;
        private int _caretPosition = 0; // Caret position in text
        private int _selectionStart = 0; // Selection start
        private int _selectionEnd = 0; // Selection end
        private bool _isSelecting = false; // Selection flag

        // Static reference to current active activator (so only one is active)
        private static SimpleInputFieldActivator _currentActive = null;

        void Start()
        {
            Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] Start called, inputField={(inputField != null ? inputField.name : "NULL")}, interactable={inputField?.interactable}");

            // Find ScrollRect in parent hierarchy
            Transform parent = transform.parent;
            while (parent != null && _scrollRect == null)
            {
                _scrollRect = parent.GetComponentInParent<ScrollRect>();
                if (_scrollRect != null) break;
                parent = parent.parent;
            }

            if (_scrollRect != null)
            {
                Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] Found ScrollRect: {_scrollRect.gameObject.name}");
            }
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] OnPointerDown called, inputField={(inputField != null ? inputField.name : "NULL")}");

            // Determine initial click position in text for selection start
            if (inputField != null && inputField.textComponent != null)
            {
                int clickPosition = GetCharacterIndexFromPosition(eventData.position);
                _caretPosition = clickPosition;
                _selectionStart = clickPosition;
                _selectionEnd = clickPosition;
                _isSelecting = false; // Start selection on drag
                UpdateCaretPosition();
                Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] PointerDown position in text: {clickPosition}");
            }

            // Use coroutine for activation delay
            StartCoroutine(ActivateInputFieldDelayed());
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] OnPointerClick called, inputField={(inputField != null ? inputField.name : "NULL")}");

            // Determine click position in text
            if (inputField != null && inputField.textComponent != null)
            {
                int clickPosition = GetCharacterIndexFromPosition(eventData.position);
                _caretPosition = clickPosition;
                _selectionStart = clickPosition;
                _selectionEnd = clickPosition;
                _isSelecting = false;
                UpdateCaretPosition();
                Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] Click position in text: {clickPosition}");
            }

            // Use coroutine for activation delay
            StartCoroutine(ActivateInputFieldDelayed());
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _isSelecting = false;
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (inputField != null && inputField.textComponent != null && _isActivated)
            {
                _isSelecting = true;
                int dragPosition = GetCharacterIndexFromPosition(eventData.position);
                _selectionEnd = dragPosition;
                _caretPosition = dragPosition;
                UpdateCaretPosition();
            }
        }

        private int GetCharacterIndexFromPosition(Vector2 screenPosition)
        {
            if (inputField == null || inputField.textComponent == null) return 0;

            Text textComponent = inputField.textComponent;
            RectTransform textRect = textComponent.GetComponent<RectTransform>();
            if (textRect == null) return 0;

            // Convert screen position to local position text
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(textRect, screenPosition, null, out localPoint);

            // Get text
            string text = inputField.text;
            if (string.IsNullOrEmpty(text)) return 0;

            // Calculate position character based on text width
            // For left alignment start from left edge
            float charWidth = textComponent.fontSize * 0.6f; // Approx character width
            float textStartX = -textRect.rect.width / 2f + 2f; // Left start of text with padding

            // Find index character
            int charIndex = Mathf.RoundToInt((localPoint.x - textStartX) / charWidth);
            charIndex = Mathf.Clamp(charIndex, 0, text.Length);

            return charIndex;
        }

        private System.Collections.IEnumerator ActivateInputFieldDelayed()
        {
            // Wait end of frame, so that other handlers events finished
            yield return null;
            yield return null; // Additional delay for reliability

            ActivateInputField();

            // Check focus after some time
            yield return new WaitForSeconds(0.1f);
            if (inputField != null)
            {
                Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] After 0.1s delay, isFocused={inputField.isFocused}, _isActivated={_isActivated}");

                // If focus lost, try activate again
                if (!inputField.isFocused && inputField.interactable && !_isActivated)
                {
                    Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] InputField lost focus, retrying activation...");
                    ActivateInputField();
                }
            }
        }

        void OnDisable()
        {
            // Deactivate on disable
            Deactivate();
        }

        void OnDestroy()
        {
            // Deactivate on destroy
            DestroyCaret();
            if (_currentActive == this)
            {
                _currentActive = null;
            }
            // ScrollRect no longer disabled, so no need to re-enable
        }

        private void ActivateInputField()
        {
            if (inputField != null && inputField.interactable)
            {
                Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] Activating InputField {inputField.name}, isFocused={inputField.isFocused}");

                // Deactivate previous active activator
                if (_currentActive != null && _currentActive != this)
                {
                    Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] Deactivating previous active InputField {_currentActive.inputField?.name}");
                    _currentActive.Deactivate();
                }

                // Set this activator as current active
                _currentActive = this;

                // DO NOT disable ScrollRect, as this blocks scroll forever
                // Instead use different approach - handle events keyboard directly
                // ScrollRect will work normally, when InputField not focused

                // In Unity 5.6 on Windows need explicitly enable IME composition mode for text input
                UnityEngine.Input.imeCompositionMode = UnityEngine.IMECompositionMode.On;

                // Set selected object BEFORE activation
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(inputField.gameObject, null);
                }

                // Direct call activation
                inputField.ActivateInputField();

                // Also try Select
                inputField.Select();

                // Force update Canvas
                UnityEngine.Canvas.ForceUpdateCanvases();

                // Set flag activation for input processing
                _isActivated = true;

                // Initialize position caret to end text (if not set click)
                if (_caretPosition == 0 && string.IsNullOrEmpty(inputField.text) == false)
                {
                    _caretPosition = inputField.text.Length;
                }
                _selectionStart = _caretPosition;
                _selectionEnd = _caretPosition;
                _isSelecting = false;

                // Create visual caret
                CreateCaret();
                UpdateCaretPosition();

                Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] After activation, isFocused={inputField.isFocused}, textComponent={(inputField.textComponent != null ? "OK" : "NULL")}, _isActivated={_isActivated}");
            }
            else
            {
                Plugin.Log?.LogWarning($"[SimpleInputFieldActivator] Cannot activate - inputField={(inputField != null ? inputField.name : "NULL")}, interactable={inputField?.interactable}");
            }
        }

        private void Deactivate()
        {
            Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] Deactivating InputField {inputField?.name}");
            _isActivated = false;

            // Destroy caret
            DestroyCaret();

            // If this was current активный, reset ref
            if (_currentActive == this)
            {
                _currentActive = null;
            }

            // ScrollRect no longer disabled, so no need to re-enable
        }

        void Update()
        {
            // Process input only if this текущий active activator
            if (_currentActive == this && _isActivated && inputField != null && inputField.textComponent != null)
            {
                // Check if InputField still selected (for additional safety)
                bool isSelected = UnityEngine.EventSystems.EventSystem.current != null &&
                                 UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == inputField.gameObject;

                if (isSelected || inputField.isFocused)
                {
                    // Process main keys
                    if (UnityEngine.Input.inputString.Length > 0)
                    {
                        string inputString = UnityEngine.Input.inputString;
                        Plugin.Log?.LogInfo($"[SimpleInputFieldActivator] Received input: '{inputString}' for {inputField.name}");

                        // Process input considering selection and position caret
                        string currentText = inputField.text;

                        // If selection exists, replace selected text
                        if (_isSelecting && _selectionStart != _selectionEnd)
                        {
                            int start = Mathf.Min(_selectionStart, _selectionEnd);
                            int end = Mathf.Max(_selectionStart, _selectionEnd);
                            currentText = currentText.Substring(0, start) + inputString + currentText.Substring(end);
                            _caretPosition = start + inputString.Length;
                        }
                        else
                        {
                            // Insert at position caret
                            _caretPosition = Mathf.Clamp(_caretPosition, 0, currentText.Length);
                            currentText = currentText.Insert(_caretPosition, inputString);
                            _caretPosition += inputString.Length;
                        }

                        inputField.text = currentText;
                        _selectionStart = _caretPosition;
                        _selectionEnd = _caretPosition;
                        _isSelecting = false;

                        // Update position caret
                        UpdateCaretPosition();

                        // Activate InputField again, to refresh display
                        inputField.ActivateInputField();
                    }

                    // Process period on NumLock (KeypadPeriod)
                    if (UnityEngine.Input.GetKeyDown(KeyCode.KeypadPeriod))
                    {
                        string currentText = inputField.text;

                        // If selection exists, replace selected text
                        if (_isSelecting && _selectionStart != _selectionEnd)
                        {
                            int start = Mathf.Min(_selectionStart, _selectionEnd);
                            int end = Mathf.Max(_selectionStart, _selectionEnd);
                            currentText = currentText.Substring(0, start) + "." + currentText.Substring(end);
                            _caretPosition = start + 1;
                        }
                        else
                        {
                            // Insert period in позицию caret
                            _caretPosition = Mathf.Clamp(_caretPosition, 0, currentText.Length);
                            currentText = currentText.Insert(_caretPosition, ".");
                            _caretPosition++;
                        }

                        inputField.text = currentText;
                        _selectionStart = _caretPosition;
                        _selectionEnd = _caretPosition;
                        _isSelecting = false;
                        UpdateCaretPosition();
                        inputField.ActivateInputField();
                    }

                    // Process Backspace
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace))
                    {
                        string currentText = inputField.text;
                        if (currentText.Length > 0)
                        {
                            // If selection exists, delete selected text
                            if (_isSelecting && _selectionStart != _selectionEnd)
                            {
                                int start = Mathf.Min(_selectionStart, _selectionEnd);
                                int end = Mathf.Max(_selectionStart, _selectionEnd);
                                currentText = currentText.Substring(0, start) + currentText.Substring(end);
                                _caretPosition = start;
                            }
                            else if (_caretPosition > 0)
                            {
                                // Delete char before caretом
                                currentText = currentText.Substring(0, _caretPosition - 1) + currentText.Substring(_caretPosition);
                                _caretPosition--;
                            }

                            inputField.text = currentText;
                            _selectionStart = _caretPosition;
                            _selectionEnd = _caretPosition;
                            _isSelecting = false;
                            UpdateCaretPosition();
                            inputField.ActivateInputField();
                        }
                    }

                    // Process Delete
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Delete))
                    {
                        string currentText = inputField.text;
                        if (currentText.Length > 0)
                        {
                            // If selection exists, delete selected text
                            if (_isSelecting && _selectionStart != _selectionEnd)
                            {
                                int start = Mathf.Min(_selectionStart, _selectionEnd);
                                int end = Mathf.Max(_selectionStart, _selectionEnd);
                                currentText = currentText.Substring(0, start) + currentText.Substring(end);
                                _caretPosition = start;
                            }
                            else if (_caretPosition < currentText.Length)
                            {
                                // Delete char after caret
                                currentText = currentText.Substring(0, _caretPosition) + currentText.Substring(_caretPosition + 1);
                            }

                            inputField.text = currentText;
                            _selectionStart = _caretPosition;
                            _selectionEnd = _caretPosition;
                            _isSelecting = false;
                            UpdateCaretPosition();
                            inputField.ActivateInputField();
                        }
                    }

                    // Process arrows for moving caret
                    if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
                    {
                        _caretPosition = Mathf.Max(0, _caretPosition - 1);
                        _selectionStart = _caretPosition;
                        _selectionEnd = _caretPosition;
                        _isSelecting = false;
                        UpdateCaretPosition();
                    }

                    if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
                    {
                        _caretPosition = Mathf.Min(inputField.text.Length, _caretPosition + 1);
                        _selectionStart = _caretPosition;
                        _selectionEnd = _caretPosition;
                        _isSelecting = false;
                        UpdateCaretPosition();
                    }
                }
                else
                {
                    // If InputField no longer selected, deactivate эthe same activator
                    Deactivate();
                }
            }
        }

        private void CreateCaret()
        {
            if (_caretObj != null || inputField == null || inputField.textComponent == null) return;

            // Create visual caret
            _caretObj = new GameObject("Caret");
            _caretObj.transform.SetParent(inputField.textComponent.transform, false);

            RectTransform caretRect = _caretObj.AddComponent<RectTransform>();
            caretRect.sizeDelta = new Vector2(2f, inputField.textComponent.fontSize * 1.2f);
            caretRect.pivot = new Vector2(0f, 0.5f); // Левая сторit caret for точного позиционирования
            caretRect.anchorMin = new Vector2(0f, 0.5f);
            caretRect.anchorMax = new Vector2(0f, 0.5f);
            caretRect.anchoredPosition = new Vector2(2f, 0f); // Небольшой отступ слева for пустого поля

            UnityEngine.UI.Image caretImage = _caretObj.AddComponent<UnityEngine.UI.Image>();
            caretImage.color = Color.white;

            // Start caret blink caret
            _caretBlinkCoroutine = StartCoroutine(CaretBlinkCoroutine());
        }

        private void UpdateCaretPosition()
        {
            if (_caretObj == null || inputField == null || inputField.textComponent == null) return;

            RectTransform caretRect = _caretObj.GetComponent<RectTransform>();
            RectTransform textRect = inputField.textComponent.GetComponent<RectTransform>();
            if (caretRect == null || textRect == null) return;

            // Position caret to end of text
            Text textComponent = inputField.textComponent;
            string text = inputField.text;

            // Force update Canvas for correct calculation preferredWidth
            UnityEngine.Canvas.ForceUpdateCanvases();

            // Ensure позиция caret in valid range
            _caretPosition = Mathf.Clamp(_caretPosition, 0, text.Length);

            if (string.IsNullOrEmpty(text) || _caretPosition == 0)
            {
                // If no text or caret at start, position caret left with small padding
                caretRect.anchoredPosition = new Vector2(2f, 0f);
            }
            else
            {
                // Calculate width text to position caret
                string textBeforeCaret = text.Substring(0, _caretPosition);
                float textWidth = 0f;

                // Method 1: Используем preferredWidth for text to caret
                try
                {
                    TextGenerator generator = new TextGenerator();
                    TextGenerationSettings settings = textComponent.GetGenerationSettings(textRect.rect.size);
                    settings.generateOutOfBounds = true;
                    settings.scaleFactor = 1f;

                    textWidth = generator.GetPreferredWidth(textBeforeCaret, settings);

                    // If failed, use simple calculation
                    if (textWidth <= 0f || float.IsNaN(textWidth) || float.IsInfinity(textWidth))
                    {
                        // Approx character width for Arial и цифр
                        float charWidth = textComponent.fontSize * 0.6f;
                        textWidth = textBeforeCaret.Length * charWidth;
                    }
                }
                catch
                {
                    // Fallback: simple calculation
                    float charWidth = textComponent.fontSize * 0.6f;
                    textWidth = textBeforeCaret.Length * charWidth;
                }

                // Position caret in specified position
                // Text aligned left edge (TextAnchor.MiddleLeft)
                caretRect.anchoredPosition = new Vector2(textWidth + 2f, 0f);
            }
        }

        private System.Collections.IEnumerator CaretBlinkCoroutine()
        {
            if (_caretObj == null) yield break;

            UnityEngine.UI.Image caretImage = _caretObj.GetComponent<UnityEngine.UI.Image>();
            if (caretImage == null) yield break;

            while (_currentActive == this && _isActivated && _caretObj != null)
            {
                // Show caret
                caretImage.color = new Color(1f, 1f, 1f, 1f);
                yield return new WaitForSeconds(0.5f);

                if (_currentActive != this || !_isActivated || _caretObj == null) break;

                // Hide caret
                caretImage.color = new Color(1f, 1f, 1f, 0f);
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void DestroyCaret()
        {
            if (_caretBlinkCoroutine != null)
            {
                StopCoroutine(_caretBlinkCoroutine);
                _caretBlinkCoroutine = null;
            }

            if (_caretObj != null)
            {
                UnityEngine.Object.Destroy(_caretObj);
                _caretObj = null;
            }
        }
    }
}
