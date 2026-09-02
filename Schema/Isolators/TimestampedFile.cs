// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;

namespace Schema.Isolators;

/// <summary>
/// One directory entry together with the write time the platform already knew while walking it.
/// <para>
/// The point of this type is what it saves rather than what it holds. A walk that yields only paths
/// throws away a timestamp the operating system handed it for free, so any caller wanting "the N most
/// recently written files here" has to go back and ask for each one — a <c>stat</c> per candidate. On
/// a 50,000-file directory that measured ~3,500&#160;ms, against ~100&#160;ms for the same ordering
/// when the walk carries the timestamp with it.
/// </para>
/// <para>
/// A <c>readonly record struct</c> on purpose: one of these exists per directory entry, and a walk of
/// tens of thousands should not allocate tens of thousands of objects to answer a question about
/// twenty of them.
/// </para>
/// </summary>
/// <param name="Path">Full path of the file, with any long-path prefix already stripped.</param>
/// <param name="LastWriteTimeUtc">The file's last write time, in UTC.</param>
public readonly record struct TimestampedFile(string Path, DateTime LastWriteTimeUtc);
