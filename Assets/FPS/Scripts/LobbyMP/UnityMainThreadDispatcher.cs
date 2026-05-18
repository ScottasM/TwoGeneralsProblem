using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static UnityMainThreadDispatcher _instance;

    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null) {
            if (Application.isPlaying) {
                // We are running on the main thread - it's safe to call FindObjectOfType
                _instance = FindObjectOfType<UnityMainThreadDispatcher>();
            }

            if (_instance == null) {
                // If it's still null, create a new GameObject
                GameObject obj = new GameObject("UnityMainThreadDispatcher");
                _instance = obj.AddComponent<UnityMainThreadDispatcher>();
            }
        }

        return _instance;
    }


    private void Update()
    {
        lock (_executionQueue) {
            while (_executionQueue.Count > 0) {
                Action action = _executionQueue.Dequeue();
                action.Invoke();
            }
        }
    }

    public void Enqueue(Action action)
    {
        lock (_executionQueue) {
            _executionQueue.Enqueue(action);
        }
    }

    public Task<T> EnqueueAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();

        Enqueue(() =>
        {
            try
            {
                T result = func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
}