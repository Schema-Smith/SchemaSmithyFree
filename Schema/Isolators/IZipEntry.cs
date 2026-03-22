using System.IO;

namespace Schema.Isolators;

public interface IZipEntry
{
    string FullName { get; }
    Stream Open();
}
