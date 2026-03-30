using System;
using System.Collections;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.UI
{
    /// <summary>
    /// Стандартная визуализация tooltip предмета.
    /// Простая карточка с названием, описанием и иконкой.
    /// Можно создавать кастомные визуализации реализовав ITooltipView.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DefaultTooltipView : BaseTooltipView
    {
        [Header("UI Elements")]
        [SerializeField, Tooltip("Текст названия предмета")]
        private TextMeshProUGUI _itemNameText;

        [SerializeField, Tooltip("Текст описания предмета")]
        private TextMeshProUGUI _itemDescriptionText;

        [SerializeField, Tooltip("Иконка предмета")]
        private Image _itemIcon;

        [Header("Animation")]
        [SerializeField, Tooltip("Использовать fade-in/out анимацию")]
        private bool _useFadeAnimation = true;
        [SerializeField, Tooltip("CanvasGroup для анимации"), ShowIf(nameof(_useFadeAnimation))]
        private CanvasGroup _canvasGroup;

        [SerializeField, Tooltip("Скорость fade-in"), ShowIf(nameof(_useFadeAnimation))]
        private float _fadeInTime = 1f;

        [SerializeField, Tooltip("Скорость fade-out"), ShowIf(nameof(_useFadeAnimation))]
        private float _fadeOutTime = 1f;

        // State
        private Coroutine _fadeCoroutine;
        private IItemAdapter _currentItemAdapter;

        // Properties
        public bool IsVisible => gameObject.activeSelf;

        private void Awake()
        {
            // Добавляем CanvasGroup если нужна анимация
            if (_useFadeAnimation)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
                _canvasGroup.alpha = 0f;
            }

            // Изначально скрываем
            gameObject.SetActive(false);
        }

        public override void Show(IItemAdapter itemAdapter, Action OnCompleted = null)
        {
            if (itemAdapter == null)
            {
                Hide();
                return;
            }

            // Заполняем содержимое
            SetContent(itemAdapter);

            // Показываем
            gameObject.SetActive(true);

            // Анимация fade-in
            if (_useFadeAnimation && _canvasGroup != null)
            {
                if (_fadeCoroutine != null)
                    StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = StartCoroutine(FadeIn(OnCompleted));
            }
        }

        public override void Hide(Action OnCompleted = null)
        {
            _currentItemAdapter = null;

            // Анимация fade-out
            if (_useFadeAnimation && _canvasGroup != null && gameObject.activeSelf)
            {
                if (_fadeCoroutine != null)
                    StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = StartCoroutine(FadeOut(OnCompleted));
            }
            else
            {
                // Мгновенно скрываем
                gameObject.SetActive(false);
                OnCompleted?.Invoke();
            }
        }

        #region Content Population

        /// <summary>
        /// Заполнить содержимое tooltip
        /// </summary>
        public override void SetContent(IItemAdapter itemAdapter)
        {
            if (itemAdapter == null) return;
            _currentItemAdapter = itemAdapter;
            
            // Иконка
            SetItemIcon(itemAdapter);
            // Название
            SetItemName(itemAdapter);
            // Описание
            SetItemDescription(itemAdapter);
        }

        /// <summary>
        /// Установить название предмета
        /// </summary>
        private void SetItemName(IItemAdapter itemAdapter)
        {
            _itemNameText.text = itemAdapter?.DisplayName;
        }

        /// <summary>
        /// Установить описание предмета
        /// </summary>
        private void SetItemDescription(IItemAdapter itemAdapter)
        {
            if (_itemDescriptionText == null)
                return;

            string description = GetDescriptionText(itemAdapter);

            if (!string.IsNullOrEmpty(description))
            {
                _itemDescriptionText.text = description;
                _itemDescriptionText.gameObject.SetActive(true);
            }
            else
            {
                _itemDescriptionText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Установить иконку предмета
        /// </summary>
        private void SetItemIcon(IItemAdapter itemAdapter)
        {
            if (_itemIcon == null)
                return;

            if (itemAdapter.Icon != null)
            {
                _itemIcon.sprite = itemAdapter.Icon;
                _itemIcon.gameObject.SetActive(true);
            }
            else
            {
                _itemIcon.gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// Получить текст описания в зависимости от формата
        /// </summary>
        private string GetDescriptionText(IItemAdapter itemAdapter)
        {
            if (itemAdapter is IDescribable describableItem)
            {
                return describableItem.Description;
            }
            return string.Empty;
        }

        #endregion

        #region Animation

        private IEnumerator FadeIn(Action OnCompleted = null)
        {
            if (_canvasGroup == null)
                yield break;
            
            if (_fadeInTime > Time.unscaledDeltaTime)
            {
                float fadeInSpeed = 1f / Mathf.Max(Time.unscaledDeltaTime, _fadeInTime);
                while (_canvasGroup.alpha < 1f)
                {
                    yield return null;
                    _canvasGroup.alpha += Time.unscaledDeltaTime * fadeInSpeed;
                }
            }

            _canvasGroup.alpha = 1f;
            OnCompleted?.Invoke();
        }

        private IEnumerator FadeOut(Action OnCompleted = null)
        {
            if (_canvasGroup == null)
            {
                gameObject.SetActive(false);
                yield break;
            }
            if (_fadeOutTime > Time.unscaledDeltaTime)
            {
                float fadeOutSpeed = 1f / Mathf.Max(Time.unscaledDeltaTime, _fadeOutTime);

                while (_canvasGroup.alpha > 0f)
                {
                    _canvasGroup.alpha -= Time.unscaledDeltaTime * fadeOutSpeed;
                    yield return null;
                }
            }

            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            OnCompleted?.Invoke();
        }

        #endregion
    }
}
