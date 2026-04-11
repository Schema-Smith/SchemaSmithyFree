// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Concurrent;

namespace Schema.Isolators;

public static class FactoryContainer
{
    private static readonly ConcurrentDictionary<Type, object> _container = new();
    public static readonly object SharedLockObject = new();

    public static T ResolveOrCreate<T>(bool registerIfNew = false)
    {
        var result = Resolve<T>();
        if (result == null)
        {
            result = Activator.CreateInstance<T>();
            if (registerIfNew)
                Register(result);
        }
        return result;
    }

    public static I ResolveOrCreate<I, T>(bool registerIfNew = false) where T : I
    {
        var result = Resolve<I, T>();
        if (result == null)
        {
            result = Activator.CreateInstance<T>();
            if (registerIfNew)
                Register(result);
        }
        return result;
    }

    public static I Resolve<I, T>() where T : I
    {
        if (_container.TryGetValue(typeof(I), out var byInterface))
            return (I)byInterface;
        if (_container.TryGetValue(typeof(T), out var byType))
            return (I)byType;
        return default;
    }

    public static T Resolve<T>()
    {
        if (_container.TryGetValue(typeof(T), out var value))
            return (T)value;
        return default;
    }

    public static void Register<T>(T value)
    {
        _container[typeof(T)] = value;
    }

    public static void Unregister<T>()
    {
        _container.TryRemove(typeof(T), out _);
    }

    public static void Clear()
    {
        _container.Clear();
    }
}
