using System.IO;
using System.Linq;
using Schema.Utility;

namespace Schema.Isolators;

public class DirectoryInfoWrapper : IDirectoryInfo
{
    private readonly DirectoryInfo _directoryInfo;
    public bool Exists => _directoryInfo.Exists;
    public string FullName => _directoryInfo.FullName;
    public string Name => _directoryInfo.Name;
    public FileAttributes Attributes => _directoryInfo.Attributes;

    public DirectoryInfoWrapper(string path)
    {
        _directoryInfo = new DirectoryInfo(LongPathSupport.MakeSafeLongFilePath(path));
    }

    public DirectoryInfoWrapper(DirectoryInfo directoryInfo)
    {
        _directoryInfo = directoryInfo;
    }

    public IFileInfo[] GetFiles(string searchPattern, SearchOption searchOption)
    {
        return _directoryInfo.GetFiles(searchPattern, searchOption)
            .GroupBy(f => f.FullName).Select(g => g.First()) // get the distinct list by FullName
            .Select(f => new FileInfoWrapper(f) as IFileInfo).ToArray();
    }

    public IFileSystemInfo[] GetFileSystemInfos()
    {
        return _directoryInfo.GetFileSystemInfos().Select(fsi => (fsi.Attributes & FileAttributes.Directory) != 0
                ? (IFileSystemInfo)new DirectoryInfoWrapper((DirectoryInfo)fsi)
                : new FileInfoWrapper((FileInfo)fsi)).ToArray();
    }
}
