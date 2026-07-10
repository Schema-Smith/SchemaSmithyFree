// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace SchemaQuench;

/// <summary>
/// Classifies an object script's type from its folder placement (#243 E5) — never from the SQL
/// body. Object folders are user-organized by convention (Procedures / Views / Functions); an
/// unrecognized folder yields the generic "objectScript". The report's scriptsRan total does not
/// depend on this classification — only the per-object Details label does.
/// </summary>
public static class ObjectScriptClassifier
{
    public static string Classify(string logPath)
    {
        var segments = (logPath ?? "").Split('/', '\\');
        foreach (var seg in segments)
        {
            var s = seg.Trim().ToLowerInvariant();
            if (s is "procedures" or "storedprocedures" or "procs") return "procedure";
            if (s is "views") return "view";
            if (s is "functions" or "funcs") return "function";
        }

        return "objectScript";
    }
}
