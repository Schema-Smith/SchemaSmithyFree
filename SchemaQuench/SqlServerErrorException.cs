// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;

namespace SchemaQuench;

/// <summary>
/// Carries the originating SQL Server error number for an error surfaced through the
/// <c>InfoMessage</c> event (which fires instead of throwing when
/// <c>FireInfoMessageEventOnUserErrors</c> is enabled, as it is for the table-quench
/// connection). A raw <see cref="Microsoft.Data.SqlClient.SqlException"/> is never thrown on
/// that path, so without this the error number is lost and only the (locale-dependent) message
/// survives. Preserving the number lets deadlock detection key on 1205 regardless of server
/// message language.
/// </summary>
internal sealed class SqlServerErrorException : Exception
{
    public int Number { get; }

    public SqlServerErrorException(int number, string message) : base(message) => Number = number;
}
