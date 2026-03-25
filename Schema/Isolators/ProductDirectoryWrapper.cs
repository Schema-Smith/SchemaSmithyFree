// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
namespace Schema.Isolators;

namespace Schema.Isolators;

public class ProductDirectoryWrapper
{
    public static IDirectory GetFromFactory()
    {
        return FactoryContainer.Resolve<ZipDirectoryWrapper>() ?? FactoryContainer.ResolveOrCreate<IDirectory, DirectoryWrapper>();
    }
}
