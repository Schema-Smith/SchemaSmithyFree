// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Npgsql;

namespace SchemaQuench;

/// <summary>
/// Classifies whether an exception is the per-script "should not apply" sentinel — a script's
/// runtime decision that it should be skipped, not failed. Recognised by an exact (trimmed,
/// case-insensitive) match of the script's raised message against <see cref="Constant"/>, walking
/// the inner-exception chain. Honoured only at error level: the classifier is consulted from the
/// script-execution catch, so by construction it only ever sees an already-error-level exception
/// (on SQL Server the sentinel arrives as the rethrown <see cref="SqlServerErrorException"/> from
/// the InfoMessage path, where only severity &gt; 10 is rethrown).
///
/// <para>Sibling of <see cref="DeadlockClassifier"/>. Canonical per-platform raises:
/// SQL Server <c>RAISERROR('SCHEMASMITH: SHOULD NOT APPLY', 16, 1)</c>,
/// PostgreSQL <c>RAISE EXCEPTION 'SCHEMASMITH: SHOULD NOT APPLY'</c>,
/// MySQL <c>SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'SCHEMASMITH: SHOULD NOT APPLY'</c>.</para>
///
/// <para><b>PostgreSQL message-wrapping note:</b> Npgsql 8+ sets
/// <c>PostgresException.Message</c> to <c>"{SqlState}: {MessageText}"</c> — e.g.
/// <c>"P0001: SCHEMASMITH: SHOULD NOT APPLY"</c>. The <c>MessageText</c> property carries the
/// bare server-raised text. This classifier checks both <c>Message</c> and — for
/// <c>PostgresException</c> specifically — <c>MessageText</c>, so both paths are covered without
/// loosening to a substring match.</para>
/// </summary>
internal static class SentinelClassifier
{
    public const string Constant = "SCHEMASMITH: SHOULD NOT APPLY";

    public static bool IsShouldNotApply(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e.Message != null && e.Message.Trim().Equals(Constant, StringComparison.OrdinalIgnoreCase))
                return true;
            if (e is PostgresException pg && pg.MessageText != null && pg.MessageText.Trim().Equals(Constant, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
