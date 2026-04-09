// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using Schema.Utility;

using SchemaSmith.Pro;
namespace Schema.Isolators;

public class FileWrapper : IFile
{
    public void Copy(string source, string destination, bool overwrite = false)
    {
        File.Copy(LongPathSupport.MakeSafeLongFilePath(source), LongPathSupport.MakeSafeLongFilePath(destination), overwrite);
    }

    public bool Exists(string path)
    {
        return File.Exists(LongPathSupport.MakeSafeLongFilePath(path));
    }

    public Stream OpenRead(string path)
    {
        return File.OpenRead(LongPathSupport.MakeSafeLongFilePath(path));
    }

    public byte[] ReadAllBytes(string path)
    {
        return File.ReadAllBytes(LongPathSupport.MakeSafeLongFilePath(path));
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(LongPathSupport.MakeSafeLongFilePath(path));
    }

    public void WriteAllText(string path, string contents)
    {
        File.WriteAllText(LongPathSupport.MakeSafeLongFilePath(path), contents);
    }

    public void Delete(string path)
    {
        File.Delete(LongPathSupport.MakeSafeLongFilePath(path));
    }

    public void Move(string sourceFileName, string destFileName)
    {
        File.Move(LongPathSupport.MakeSafeLongFilePath(sourceFileName), LongPathSupport.MakeSafeLongFilePath(destFileName));
    }

    public string[] ReadAllLines(string path)
    {
        return File.ReadAllLines(LongPathSupport.MakeSafeLongFilePath(path));
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        return File.GetLastWriteTimeUtc(LongPathSupport.MakeSafeLongFilePath(path));
    }

    public static IFile GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<IFile, FileWrapper>();
    }
}
