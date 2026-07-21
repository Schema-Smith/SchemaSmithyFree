# Module 4 — Bring an existing database under management (lab)

Goal: take a database that nobody has under source control, point **SchemaTongs** at it, and cast it
into a schema package — tables, keys, and indexes — that you then own and manage like any other.

Each engine folder has:
- `seed.sql` — plain DDL (+ a little data) that stands up a small `chinook` music-catalog database.
  This is **not** a SchemaSmith package; it's the "legacy" database you're bringing under management.
- `tongs.settings.json` — the SchemaTongs config: where to connect and where to write the package.
- `solution/` — the package SchemaTongs produces, to compare your extraction against.

It's a trimmed slice of the classic Chinook schema: `Artist`, `Album`, `Track`, `Genre`,
`MediaType`, `Playlist`, and `PlaylistTrack` (7 tables, real foreign keys).

## Before you start

- The [sandbox](../docker) is up (`docker compose up -d`).
- The CLI is on your PATH (`schematongs --version`).

## Step 1: Stand up the "existing" database

Load `seed.sql` into the sandbox. It creates a `chinook` database and the seven tables.

```bash
# SQL Server
docker exec -i learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C < sqlserver/seed.sql

# PostgreSQL  (the seed CREATEs the chinook database, then \c switches into it)
docker exec -i learn-postgres psql -U postgres -d learn < postgres/seed.sql

# MySQL
docker exec -i learn-mysql mysql -uroot -pLearn!Passw0rd < mysql/seed.sql

# MariaDB
docker exec -i learn-mariadb mariadb -uroot -pLearn!Passw0rd < mariadb/seed.sql
```

Spot-check it's there (SQL Server shown): `SELECT name FROM chinook.sys.tables;` — seven tables, none
of them yours yet.

## Step 2: Cast it into a package

From your engine folder:

```bash
cd <engine>
schematongs --ConfigFile:tongs.settings.json
```

Expected:

```
  Cast Json for dbo.Album
  Cast Json for dbo.Artist
  ... (and the rest)

=== Casting Summary ===
  Tables:     7 extracted, 0 errors
Casting Completed Successfully
```

SchemaTongs writes the package to `./Extracted`:

```
Extracted/
  Product.json
  Templates/Chinook/Tables/<schema>.Album.json
  Templates/Chinook/Tables/<schema>.Artist.json
  ... (one file per table)
```

## Step 3: See what it caught

Open `Extracted/Templates/Chinook/Tables/<schema>.Album.json`. SchemaTongs didn't just record columns —
it pulled the **primary key**, the **index** on the artist column, and the **foreign key** linking
`Album` back to `Artist`, straight from the live catalog. Those relationships are the first thing you'd
miss transcribing a schema by hand.

Per-engine differences are cosmetic: SQL Server quotes with `[brackets]` and reports `INT IDENTITY`;
PostgreSQL folds names to lowercase (`public.album.json`) and reports types like `int4`; MySQL quotes
with `` `backticks` `` and records `AutoIncrement` plus the storage engine. MariaDB matches MySQL's
quoting and casing here — it's a fourth platform in the MySQL family, not the MySQL package retargeted.

## Step 4: Compare against the solution

Your `Extracted/` package should match `solution/`. From here, it's a normal SchemaSmith package — you
manage it exactly like the one you built in Module 2: edit a table, preview with WhatIf, and quench.

## Reset

To start over, drop the database and re-run the seed:

```bash
# SQL Server
docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -Q "DROP DATABASE chinook"
# PostgreSQL
docker exec learn-postgres psql -U postgres -d learn -c "DROP DATABASE chinook"
# MySQL
docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -e "DROP DATABASE chinook"
# MariaDB
docker exec learn-mariadb mariadb -uroot -pLearn!Passw0rd -e "DROP DATABASE chinook"
```
