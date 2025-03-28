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
        I result = default;
        if (_container.ContainsKey(typeof(I)))
            result = (I)_container[typeof(I)];
        else if (_container.ContainsKey(typeof(T)))
            result = (I)_container[typeof(T)];
        return result;
    }

    public static T Resolve<T>()
    {
        T result = default;
        if (_container.ContainsKey(typeof(T)))
            result = (T)_container[typeof(T)];
        return result;
    }

    public static void Register<T>(T value)
    {
        if (_container.ContainsKey(typeof(T)))
            _container[typeof(T)] = value;
        else
            _container.TryAdd(typeof(T), value);
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