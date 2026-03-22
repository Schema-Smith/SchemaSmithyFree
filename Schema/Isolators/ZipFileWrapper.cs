// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Schema.Utility;

namespace Schema.Isolators;

public class ZipFileWrapper : IFile, IDisposable
{
    public List<IZipEntry> ZipEntries { get; private set; }

    private string _zipFilePath;
    private ZipArchive _archive;
    private readonly object _lockObject = new();

    public bool Exists(string path)
    {
        if (string.IsNullOrEmpty(NormalizePath(path)))
            return false;

        return ZipEntries.Any(e => NormalizePath(e.FullName).EqualsIgnoringCase(NormalizePath(path)));
    }

    public string ReadAllText(string path)
    {
        if (string.IsNullOrEmpty(NormalizePath(path)))
            throw new FileNotFoundException($"Invalid entry requested: '{path}'");

        lock (_lockObject)
        {
            var entry = ZipEntries.FirstOrDefault(e => NormalizePath(e.FullName).EqualsIgnoringCase(NormalizePath(path)));
            if (entry == null)
                throw new FileNotFoundException($"Entry '{path}' not found in zip file '{_zipFilePath}'.");

            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    public static bool IsValidZipFile(string filename)
    {
        var file = FileWrapper.GetFromFactory();
        if (string.IsNullOrEmpty(filename) || !file.Exists(filename))
            return false;

        try
        {
            using var fileStream = file.OpenRead(filename);
            if (fileStream.Length == 0) return false;
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);
            _ = archive.Entries;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Initialize(string zipFileName)
    {
        _zipFilePath = zipFileName;
        lock (_lockObject)
        {
            _archive = ZipFile.OpenRead(zipFileName);
            ZipEntries = _archive.Entries.Select(e => (IZipEntry)new ZipEntryWrapper(e)).ToList();
        }
    }

    private static string NormalizePath(string path)
    {
        return path?.Replace('\\', '/').Trim('/');
    }

    public static IFile GetFromFactory(string zipFileName)
    {
        var zipFile = FactoryContainer.ResolveOrCreate<ZipFileWrapper>(true);
        zipFile.Initialize(zipFileName);
        return zipFile;
    }

    internal void SetZipEntries(List<IZipEntry> entries)
    {
        ZipEntries = entries;
    }

    public void Dispose()
    {
        ZipEntries = null;
        _archive?.Dispose();
    }

    // Other IFile methods not used for zip access
    public Stream OpenRead(string path) => throw new NotImplementedException();
    public void WriteAllText(string path, string contents) => throw new NotImplementedException();
    public void Copy(string source, string destination, bool overwrite = false) => throw new NotImplementedException();
    public void Delete(string path) => throw new NotImplementedException();
}
