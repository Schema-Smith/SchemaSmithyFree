// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;

namespace Schema.Isolators;

public interface IFile
{
    void Copy(string source, string destination, bool overwrite = false);
    void Delete(string path);
    void Move(string sourceFileName, string destFileName);
    bool Exists(string path);
    Stream OpenRead(string path);
    byte[] ReadAllBytes(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);
    string[] ReadAllLines(string path);
    DateTime GetLastWriteTimeUtc(string path);
}
