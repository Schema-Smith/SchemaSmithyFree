// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Schema.Utility;

public class TaskQueueManager<T> : IDisposable
{
    private readonly int _maxTasks;
    private readonly List<WorkerTask> _workingTasks = [];
    private readonly Queue<WorkerTask> _workQueue = new();
    private readonly object _lockObject = new();

    public TaskQueueManager(int maxTasks = 20)
    {
        _maxTasks = maxTasks;
        if (_maxTasks < 1) _maxTasks = 1;
    }

    ~TaskQueueManager() => Dispose();

    public void Dispose()
    {
        lock (_lockObject)
        {
            _workQueue.Clear();
            while (_workingTasks.Count > 0) _workingTasks.Remove(_workingTasks[0]);
        }
    }

    public delegate void TaskDelegate(T item);

    public void AddToQueue(T item, TaskDelegate workProcedure)
    {
        var aWorker = new WorkerTask(item, workProcedure, this);
        ProcessQueue();
        lock (_lockObject)
        {
            if (_workingTasks.Count < _maxTasks)
                StartTask(aWorker);
            else
                _workQueue.Enqueue(aWorker);
        }
    }

    private void TaskComplete(WorkerTask aTask)
    {
        lock (_lockObject)
        {
            _workingTasks.Remove(aTask);
            ProcessQueue();
        }
    }

    private void StartTask(WorkerTask aTask)
    {
        lock (_lockObject)
        {
            _workingTasks.Add(aTask);
            aTask.StartTask();
        }
    }

    private void ProcessQueue()
    {
        lock (_lockObject)
        {
            while (_workingTasks.Count < _maxTasks && _workQueue.Count > 0)
            {
                StartTask(_workQueue.Peek());
                _workQueue.Dequeue();
            }
        }
    }

    public void WaitForAll(Action action = null)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        while (_workingTasks.Count > 0 || _workQueue.Count > 0)
        {
            ProcessQueue();
            Thread.Sleep(100);
            action?.Invoke();
        }
    }

    private class WorkerTask(T item, TaskDelegate workProcedure, TaskQueueManager<T> owner)
    {
        private Task _task;

        private void DoWork()
        {
            workProcedure(item);
            owner.TaskComplete(this);
        }

        public void StartTask()
        {
            if (_task != null) return;
            _task = new Task(DoWork);
            _task.Start();
        }
    }
}
