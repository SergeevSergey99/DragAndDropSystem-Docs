using System;
using UnityEngine;
using UDND.Tools;

namespace UDND.UI
{
    public abstract class FadeTooltipView : BaseTooltipView
    {
        [SerializeField, Tooltip("CanvasGroup for animation")]
        private CanvasGroup _canvasGroup;
        
        [SerializeField, Tooltip("Fade-in speed")]
        private float _fadeInTime = 1f;

        [SerializeField, Tooltip("Fade-out speed")]
        private float _fadeOutTime = 1f;

        protected void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            _canvasGroup.alpha = 0f;
        }
        
        protected override void ShowView(Action OnCompleted)
        {
            gameObject.SetActive(true);

            // Fade-in animation
            if (_canvasGroup != null)
            {
                _canvasGroup.FadeTo(1f, _fadeInTime, OnCompleted);
            }
            else
            {
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 1f;

                OnCompleted?.Invoke();
            }
        }

        protected override void HideView(Action OnCompleted)
        {
            // Fade-out animation
            if (_canvasGroup != null && gameObject.activeSelf)
            {
                _canvasGroup.FadeTo(0f, _fadeOutTime, () => {
                    gameObject.SetActive(false);
                    OnCompleted?.Invoke();
                });
            }
            else
            {
                // Hide immediately
                gameObject.SetActive(false);
                OnCompleted?.Invoke();
            }
        }
    }
}