using System.IO;
using Schema.Utility;

namespace Schema.Isolators;

public class FileWrapper : IFile
{
    public void Copy(string source, string destination, bool overwrite = false)
    {
        File.Copy(LongPathSupport.MakeSafeLongFilePath(source), LongPathSupport.MakeSafeLongFilePath(destination), overwrite);
    }

    public bool Exists(string path)
    {
        return File.Exists(path);
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    public void WriteAllText(string path, string contents)
    {
        File.WriteAllText(LongPathSupport.MakeSafeLongFilePath(path), contents);
    }

    public void Delete(string path)
    {
        File.Delete(path);
    }

    public static IFile GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IFile, FileWrapper>();
    }
}