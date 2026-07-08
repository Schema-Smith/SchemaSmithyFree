# Error Codes & Reporting Channels

When a deployment fails, the *same* logical problem — an orphaned foreign key, a duplicate value, a null in a required column — surfaces differently on SQL Server, PostgreSQL, and MySQL. The error text differs, the numeric code differs, and even *which log file* carries the detail differs. This page is the lookup table: how each engine reports back, where to read it, and what the codes mean across all three platforms.

---

## The three reporting channels

SchemaSmith runs the same convergence engine on every database, but each engine hands progress and errors back through a different mechanism. Knowing which one you're reading saves time when a deploy stops.

| Engine | Channel | How it works |
| --- | --- | --- |
| **SQL Server** | `InfoMessage` events | The engine raises messages on the connection; SchemaSmith promotes them to the log. |
| **PostgreSQL** | `Notice` stream | Server notices flow to the log; routine "already exists, skipping" / "does not exist, skipping" noise is filtered out. |
| **MySQL** | `SchemaSmith_StatusMessages` sidecar | Progress is written to a table and polled — MySQL has no async message event. |

> **MySQL:** MySQL runs the whole deployment on a single connection and its driver has no equivalent of the SQL Server `InfoMessage` or PostgreSQL `Notice` event, so it can't push a message while that connection is busy doing work. Instead, the engine writes progress rows to a `SchemaSmith_StatusMessages` table and a separate polling connection flushes them to the log every few hundred milliseconds. The rows are cleaned up when the run ends, so the durable record is the progress log, not the table.

## Where errors are logged

SchemaSmith writes two logs per run — a progress log (everything that happened) and an errors log (the faults). Which one carries the fault detail depends on the engine, and this catches people out.

> **SQL Server:** the errors log is populated — each fault lands there with its message and the line number (`at Line: N`), in addition to the progress log. It's the fastest place to read what went wrong.

> **PostgreSQL and MySQL:** the errors log is **empty**. The fault detail — including the SQLSTATE on PostgreSQL — is in the *progress* log only. Don't go looking in the errors log; it won't be there.

## VerboseLogging

`VerboseLogging` is a top-level configuration switch (a boolean, default `false`) that affects **SQL Server only**. SQL Server scripts commonly emit `PRINT` statements and low-severity warnings; SchemaSmith suppresses that chatter by default so the deployment log stays readable, and `VerboseLogging` is the dial that brings it back when you want to see it.

With it off (the default), a `PRINT` or a low-severity `RAISERROR` from one of your scripts is filtered out of the log. Turn it on and those messages surface. It affects **your scripts' output only** — SchemaSmith's own phase progress always shows regardless (the engine emits its progress with an always-surface flag), so turning `VerboseLogging` on won't reveal any hidden SchemaSmith progress; it only unmutes your scripts.

> **PostgreSQL and MySQL:** the switch has no effect. PostgreSQL surfaces its `RAISE NOTICE` output by default, and MySQL user scripts have no progress channel at all — so there's nothing for the dial to gate on either engine.

Set it in the settings file (`"VerboseLogging": true`), as an environment variable (`SmithySettings_VerboseLogging=true`), or on the command line:

```bash
SchemaQuench --VerboseLogging=true
```

> **Note:** Use the `=` form on the command line. `--VerboseLogging=true` is the config-override syntax and takes effect; the colon form `--VerboseLogging:true` is silently ignored for configuration settings (it applies only to the tool's named switches like `--ConfigFile` and `--LogPath`). The settings-file and environment-variable forms always work.

## Per-platform error codes

The same failure carries a different code on each engine. SQL Server usually prints the *message* (the number isn't always shown); PostgreSQL prints the SQLSTATE literally; MySQL prints its classic error message. Use this table to recognize the same fault across platforms.

| Failure | SQL Server | PostgreSQL | MySQL |
| --- | --- | --- | --- |
| Foreign-key violation (orphan) | `547` | `23503` | `1452` |
| NOT NULL violation | `515` | `23502` | `1048` |
| Duplicate / unique-key | `2601` / `2627` | `23505` | `1062` |
| String or binary truncation | `8152` | `22001` | `1406` |
| Type / conversion mismatch | `245` / `8115` | `22P02` | `1366` |
| Deadlock (retried automatically) | `1205` | `40P01` | — (message-matched) |

> **Note:** Deadlocks are retried for you — SchemaSmith detects the deadlock code and re-runs the operation with backoff, so a transient lock collision resolves itself rather than failing the deploy.

For the hands-on method — locating the failing phase, reading the artifact it leaves behind, and choosing a recovery — see the troubleshooting guide.
