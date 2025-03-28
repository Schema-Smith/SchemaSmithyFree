using System.IO;
using Schema.Utility;

namespace Schema.Isolators;

public class DirectoryWrapper : IDirectory
{
    public bool Exists(string path)
    {
        return Directory.Exists(LongPathSupport.MakeSafeLongFilePath(path));
    }

    public IDirectoryInfo CreateDirectory(string path)
    {
        return new DirectoryInfoWrapper(Directory.CreateDirectory(LongPathSupport.MakeSafeLongFilePath(path)));
    }

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.GetFiles(LongPathSupport.MakeSafeLongFilePath(path), searchPattern, searchOption);
    }

    public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.GetDirectories(path, searchPattern, searchOption); 
    }

    public void Delete(string path, bool recursive = false)
    {
        Directory.Delete(path, recursive);
    }

    public static IDirectory GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IDirectory, DirectoryWrapper>();
    }
}