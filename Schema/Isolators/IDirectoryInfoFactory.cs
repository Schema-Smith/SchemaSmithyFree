namespace Schema.Isolators;

public interface IDirectoryInfoFactory
{
    IDirectoryInfo GetDirectoryInfoWrapper(string path);
}