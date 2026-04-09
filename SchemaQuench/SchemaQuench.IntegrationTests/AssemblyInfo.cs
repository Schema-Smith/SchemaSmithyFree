// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;

// Limit parallel test execution to avoid exceeding MySQL max_connections (151)
// Each test opens a connection, and with many fixtures running in parallel,
// the connection count can easily exceed the server limit.
[assembly: LevelOfParallelism(2)]
