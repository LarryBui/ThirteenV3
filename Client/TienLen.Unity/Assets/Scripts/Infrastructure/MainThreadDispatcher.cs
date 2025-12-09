using UnityEngine;
using System.Collections.Generic;
using System;

namespace TienLen.Unity.Infrastructure
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();

        public void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (FindFirstObjectByType<MainThreadDispatcher>() == null)
            {
                var go = new GameObject("MainThreadDispatcher");
                go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }
    }
}
