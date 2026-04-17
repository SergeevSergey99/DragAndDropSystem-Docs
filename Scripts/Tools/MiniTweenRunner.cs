using System;
using System.Collections;
using System.Collections.Generic;
using CodeUtils;
using UnityEngine;

namespace UniversalDragAndDrop.Tools
{
    public enum MiniTweenEase
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic
    }

    /// <summary>
    /// Lightweight runtime tween runner used by the package to avoid external tween dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniTweenRunner : MonoSingleton<MiniTweenRunner>
    {
        private sealed class ActiveTween
        {
            public Coroutine Coroutine;
            public Action OnInterrupted;
        }

        private readonly Dictionary<UnityEngine.Object, ActiveTween> _activeTweens = new Dictionary<UnityEngine.Object, ActiveTween>();

        public void AnimatePosition(
            RectTransform target,
            Vector3 startPosition,
            Vector3 endPosition,
            float duration,
            MiniTweenEase ease,
            Action<float, Vector3> applyPosition,
            Action onComplete = null,
            Action onInterrupted = null,
            Func<float, Vector3> customPath = null)
        {
            if (target == null)
            {
                onInterrupted?.Invoke();
                return;
            }

            StartManagedTween(target, AnimatePositionCoroutine(
                target,
                startPosition,
                endPosition,
                duration,
                ease,
                applyPosition,
                onComplete,
                onInterrupted,
                customPath), onInterrupted);
        }

        public void AnimateCanvasGroupAlpha(
            CanvasGroup target,
            float startAlpha,
            float endAlpha,
            float duration,
            MiniTweenEase ease = MiniTweenEase.Linear,
            Action onComplete = null,
            Action onInterrupted = null)
        {
            if (target == null)
            {
                onInterrupted?.Invoke();
                return;
            }

            StartManagedTween(target, AnimateCanvasGroupAlphaCoroutine(
                target,
                startAlpha,
                endAlpha,
                duration,
                ease,
                onComplete,
                onInterrupted), onInterrupted);
        }

        public void StopAnimation(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (_activeTweens.TryGetValue(target, out var activeTween))
            {
                _activeTweens.Remove(target);

                if (activeTween.Coroutine != null)
                    StopCoroutine(activeTween.Coroutine);

                activeTween.OnInterrupted?.Invoke();
            }
        }

        private void StartManagedTween(UnityEngine.Object target, IEnumerator routine, Action onInterrupted)
        {
            StopAnimation(target);

            var activeTween = new ActiveTween
            {
                OnInterrupted = onInterrupted
            };

            activeTween.Coroutine = StartCoroutine(RunManagedTween(target, activeTween, routine));
            _activeTweens[target] = activeTween;
        }

        private IEnumerator RunManagedTween(UnityEngine.Object target, ActiveTween activeTween, IEnumerator routine)
        {
            yield return routine;

            if (target != null && _activeTweens.TryGetValue(target, out var currentTween) && ReferenceEquals(currentTween, activeTween))
                _activeTweens.Remove(target);
        }

        private IEnumerator AnimatePositionCoroutine(
            RectTransform target,
            Vector3 startPosition,
            Vector3 endPosition,
            float duration,
            MiniTweenEase ease,
            Action<float, Vector3> applyPosition,
            Action onComplete,
            Action onInterrupted,
            Func<float, Vector3> customPath)
        {
            if (applyPosition == null)
            {
                onInterrupted?.Invoke();
                yield break;
            }

            float safeDuration = Mathf.Max(0.0001f, duration);
            float elapsed = 0f;

            applyPosition(0f, startPosition);

            while (elapsed < safeDuration)
            {
                if (target == null)
                {
                    onInterrupted?.Invoke();
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / safeDuration);
                float easedTime = EvaluateEase(ease, normalizedTime);
                Vector3 position = customPath != null
                    ? customPath(easedTime)
                    : Vector3.LerpUnclamped(startPosition, endPosition, easedTime);

                applyPosition(easedTime, position);
                yield return null;
            }

            if (target == null)
            {
                onInterrupted?.Invoke();
                yield break;
            }

            applyPosition(1f, customPath != null ? customPath(1f) : endPosition);
            onComplete?.Invoke();
        }

        private IEnumerator AnimateCanvasGroupAlphaCoroutine(
            CanvasGroup target,
            float startAlpha,
            float endAlpha,
            float duration,
            MiniTweenEase ease,
            Action onComplete,
            Action onInterrupted)
        {
            if (target == null)
            {
                onInterrupted?.Invoke();
                yield break;
            }

            target.alpha = startAlpha;

            if (duration <= 0f)
            {
                target.alpha = endAlpha;
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (target == null)
                {
                    onInterrupted?.Invoke();
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float easedTime = EvaluateEase(ease, normalizedTime);
                float alpha = Mathf.LerpUnclamped(startAlpha, endAlpha, easedTime);

                target.alpha = alpha;
                yield return null;
            }

            if (target == null)
            {
                onInterrupted?.Invoke();
                yield break;
            }

            target.alpha = endAlpha;
            onComplete?.Invoke();
        }

        private static float EvaluateEase(MiniTweenEase ease, float t)
        {
            switch (ease)
            {
                case MiniTweenEase.InQuad:
                    return t * t;
                case MiniTweenEase.OutQuad:
                    return 1f - ((1f - t) * (1f - t));
                case MiniTweenEase.InOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case MiniTweenEase.InCubic:
                    return t * t * t;
                case MiniTweenEase.OutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case MiniTweenEase.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                default:
                    return t;
            }
        }
    }

    public static class MiniTween
    {
        public static void FadeTo(this CanvasGroup canvasGroup, float toAlpha, float duration, Action OnCompleted = null)
        {
            MiniTweenRunner.AutoCreateInstance.AnimateCanvasGroupAlpha(
                canvasGroup, canvasGroup.alpha, toAlpha, duration, onComplete: OnCompleted);
        }
    }
}
