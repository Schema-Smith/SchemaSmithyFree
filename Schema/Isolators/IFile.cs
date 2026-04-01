// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;

namespace Schema.Isolators;

public interface IFile
{
    void Copy(string source, string destination, bool overwrite = false);
    void Delete(string path);
    bool Exists(string path);
    Stream OpenRead(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);
}
