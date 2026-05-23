// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Diagnostics;

namespace SchemaQuench.IntegrationTests.PostgreSQL.Profiling;

/// <summary>
/// Heuristic stack-walking classifier used by ProfilingConnection to attribute each Open/Close
/// event to a coarse category (test-setup, test-cleanup, test-assertion, engine class name, or
/// "other"). Goal is "good enough to disentangle test from engine," not perfect attribution.
/// </summary>
internal static class ConnectionCallerClassifier
{
    private static readonly string[] EngineTypes = { "DatabaseQuench", "ProductQuench", "WorkUnitDispatcher" };
    private static readonly string[] TestSetupHints = { "FixtureSetup", "SetUp", "OneTimeSetUp", "ResetTracking", "ResetDemoState", "Initialize" };
    private static readonly string[] TestCleanupHints = { "TearDown", "OneTimeTearDown", "Drop", "Cleanup" };
    private static readonly string[] TestAssertionHints = { "AssertTableExists", "AssertProcedureExists", "ScalarCount", "AssertColumnExists" };

    public static string Classify(out string callerFrame)
    {
        var stack = new StackTrace(skipFrames: 2, fNeedFileInfo: false);
        var firstUserFrame = string.Empty;

        for (var i = 0; i < stack.FrameCount; i++)
        {
            var frame = stack.GetFrame(i);
            var method = frame?.GetMethod();
            if (method == null) continue;
            var typeName = method.DeclaringType?.Name ?? string.Empty;
            var methodName = method.Name ?? string.Empty;
            var full = $"{typeName}.{methodName}";

            if (IsInstrumentationFrame(typeName)) continue;
            if (string.IsNullOrEmpty(firstUserFrame)) firstUserFrame = full;
            if (IsAsyncMachineryFrame(typeName)) continue;

            foreach (var t in EngineTypes)
            {
                if (typeName.Contains(t)) { callerFrame = full; return t; }
            }

            if (MatchesAny(methodName, typeName, TestSetupHints)) { callerFrame = full; return "test-setup"; }
            if (MatchesAny(methodName, typeName, TestCleanupHints)) { callerFrame = full; return "test-cleanup"; }
            if (MatchesAny(methodName, typeName, TestAssertionHints)) { callerFrame = full; return "test-assertion"; }
        }

        callerFrame = firstUserFrame;
        return "other";
    }

    private static bool IsInstrumentationFrame(string typeName) =>
        typeName == nameof(ConnectionCallerClassifier) ||
        typeName == nameof(ProfilingConnection) ||
        typeName == nameof(ProfilingPostgreSqlConnectionFactory);

    private static bool IsAsyncMachineryFrame(string typeName) =>
        typeName.Contains("StateMachine") ||
        typeName.Contains("AsyncTaskMethodBuilder") ||
        typeName.Contains("AsyncValueTaskMethodBuilder");

    private static bool MatchesAny(string methodName, string typeName, string[] hints)
    {
        foreach (var h in hints)
        {
            if (methodName.Contains(h) || typeName.Contains(h)) return true;
        }
        return false;
    }
}
