namespace Schema.Isolators;

public class ProductDirectoryWrapper
{
    public static IDirectory GetFromFactory()
    {
        return FactoryContainer.Resolve<ZipDirectoryWrapper>() ?? FactoryContainer.ResolveOrCreate<IDirectory, DirectoryWrapper>();
    }
}