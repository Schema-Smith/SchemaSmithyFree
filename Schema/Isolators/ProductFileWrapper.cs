// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaSmith.Pro;
namespace Schema.Isolators;

public class ProductFileWrapper
{
    public static IFile GetFromFactory()
    {
        return FactoryContainer.Resolve<ZipFileWrapper>() ?? FactoryContainer.ResolveOrCreate<IFile, FileWrapper>();
    }
}
