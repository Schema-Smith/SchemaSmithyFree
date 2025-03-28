using System;

namespace Schema.Isolators;

public class EnvironmentWrapper : IEnvironment
{
    public void Exit(int exitCode)
    {
        Environment.Exit(exitCode);
    }

    public static IEnvironment GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IEnvironment, EnvironmentWrapper>();
    }
}