using System.IO;

namespace Schema.Isolators;

public class FileInfoWrapper(FileInfo fileInfo) : IFileInfo
{
    private readonly FileInfo _fileInfo = fileInfo;
    public bool Exists => _fileInfo.Exists;
    public string FullName => _fileInfo.FullName;
    public string Name => _fileInfo.Name;
    public FileAttributes Attributes => _fileInfo.Attributes;

    public void Delete()
    {
        _fileInfo.Delete();
    }
}