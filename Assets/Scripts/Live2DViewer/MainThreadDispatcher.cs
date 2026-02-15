using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Live2DViewer
{
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public void Enqueue(Action action)
        {
            if (action == null) return;
            _queue.Enqueue(action);
        }

        private void Update()
        {
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Dispatcher] {ex}");
                }
            }
        }
    }
}
