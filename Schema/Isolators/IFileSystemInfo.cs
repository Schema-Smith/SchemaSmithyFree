using System.IO;

namespace Schema.Isolators;

public interface IFileSystemInfo
{
    FileAttributes Attributes { get; }
    bool Exists { get; }
    string FullName { get; }
    string Name { get; }
}
