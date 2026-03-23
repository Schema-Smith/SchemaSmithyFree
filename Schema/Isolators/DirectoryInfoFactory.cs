// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
namespace Schema.Isolators;

﻿namespace Schema.Isolators;

public class DirectoryInfoFactory : IDirectoryInfoFactory
{
    public IDirectoryInfo GetDirectoryInfoWrapper(string path)
    {
        return new DirectoryInfoWrapper(path);
    }

    public static IDirectoryInfoFactory GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IDirectoryInfoFactory, DirectoryInfoFactory>();
    }
}