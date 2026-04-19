// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;

// Serial execution. Parallelism is not the cause of the ProductQuench checkpoint-on-failed-
// template bug (fixed via explicit HasCompleted + MarkStepCompleted in ProductQuench.cs),
// but LevelOfParallelism(2) exposes a separate class of flakes in the MySQL
// `[Parallelizable(ParallelScope.All)]` TableQuench_* fixtures — transient deadlock / setup
// races that have nothing to do with the shipped product. Serial execution adds about a
// minute per IT run and trades throughput for CI stability. Revisit if/when the individual
// fixture-level parallel-scope races are audited and locked.
[assembly: LevelOfParallelism(1)]
