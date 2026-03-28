using System;
using System.Collections;
using CodeUtils;
using UnityEngine;

namespace DragAndDropSystem.Tools
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
        public Coroutine AnimatePosition(
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
                return null;
            }

            return StartCoroutine(AnimatePositionCoroutine(
                target,
                startPosition,
                endPosition,
                duration,
                ease,
                applyPosition,
                onComplete,
                onInterrupted,
                customPath));
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
}
