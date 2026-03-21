using Schema.Domain;

namespace Schema.Utility;

public static class VersionHelper
{
    public static bool MeetsVersionThreshold(SqlServerVersion? minimumVersion, SqlServerVersion requiredVersion)
    {
        if (minimumVersion == null) return true;
        return minimumVersion >= requiredVersion;
    }
}
