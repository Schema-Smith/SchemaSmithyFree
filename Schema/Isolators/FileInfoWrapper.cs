using System.IO;

namespace Schema.Isolators;

public class FileInfoWrapper(FileInfo fileInfo) : IFileInfo
{
    public bool Exists => fileInfo.Exists;
    public string FullName => fileInfo.FullName;
    public string Name => fileInfo.Name;
    public FileAttributes Attributes => fileInfo.Attributes;

    public void Delete()
    {
        fileInfo.Delete();
    }
}