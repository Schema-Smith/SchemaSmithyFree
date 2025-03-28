namespace Schema.Isolators;

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