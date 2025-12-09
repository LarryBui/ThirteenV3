using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TienLen.Unity.Presentation
{
    // A simple enum for common easing types
    public enum Easing
    {
        Linear,
        OutQuad,
        InQuad,
        InOutQuad,
        OutCubic,
        InCubic,
        InOutCubic
    }

    public static class UnityExtensions
    {
        /// <summary>
        /// Smoothly moves a Transform to a target position over a given duration.
        /// </summary>
        public static async UniTask MoveToAsync(this Transform transform, Vector3 targetPosition, float duration, Easing easeType = Easing.Linear)
        {
            float timer = 0f;
            Vector3 startPosition = transform.position;

            while (timer < duration)
            {
                float t = timer / duration;
                float easedT = GetEasedValue(t, easeType);
                transform.position = Vector3.Lerp(startPosition, targetPosition, easedT);
                timer += Time.deltaTime;
                await UniTask.Yield(); // Wait for the next frame
            }
            transform.position = targetPosition; // Ensure it lands precisely
        }

        // Basic easing function implementations
        private static float GetEasedValue(float t, Easing easeType)
        {
            switch (easeType)
            {
                case Easing.Linear:
                    return t;
                case Easing.InQuad:
                    return t * t;
                case Easing.OutQuad:
                    return 1 - (1 - t) * (1 - t);
                case Easing.InOutQuad:
                    return t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
                case Easing.InCubic:
                    return t * t * t;
                case Easing.OutCubic:
                    return 1 - Mathf.Pow(1 - t, 3);
                case Easing.InOutCubic:
                    return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
                default:
                    return t;
            }
        }
    }
}
