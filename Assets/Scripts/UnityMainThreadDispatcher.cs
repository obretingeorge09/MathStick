using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    static readonly Queue<Action> queue = new Queue<Action>();
    static UnityMainThreadDispatcher instance;

    public static void Enqueue(Action action)
    {
        lock (queue) queue.Enqueue(action);
    }

    void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    void Update()
    {
        while (queue.Count > 0)
        {
            Action action;
            lock (queue) action = queue.Dequeue();
            action?.Invoke();
        }
    }
}
