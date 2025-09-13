namespace Schema.Isolators;

public class ProductFileWrapper
{
    public static IFile GetFromFactory()
    {
        return FactoryContainer.Resolve<ZipFileWrapper>() ?? FactoryContainer.ResolveOrCreate<IFile, FileWrapper>();
    }
}