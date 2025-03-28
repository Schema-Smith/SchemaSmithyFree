using System.IO;

namespace Schema.Isolators;

public interface IDirectoryInfo : IFileSystemInfo
{
    IFileInfo[] GetFiles(string searchPattern, SearchOption searchOption);
    IFileSystemInfo[] GetFileSystemInfos();
}