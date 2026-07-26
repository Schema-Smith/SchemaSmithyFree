# Course 3, Module 5 — Capstone: a careless rename, and the safety net that saves it (lab)

Goal: one continuous story, start to finish. Build `OrdersService`, **promote** it dev to staging to
prod exactly as in Module 4, then watch someone ship a **careless declarative rename** that looks clean
in dev and staging — and turns into a silent drop-and-recreate the moment it reaches prod, where the
data actually lives. The **recyclebin** from Module 3 catches the dropped table before it's gone for
good, and rolling back to the known-good package **automatically restores it, rows and all**. The
takeaway: a rename that isn't expressed as a rename is a drop in disguise, and the recyclebin is what
turns that mistake into a bad afternoon instead of a lost table.

The spine, recapped:

- **Module 2** taught the `{{TargetDb}}` script-token + `SmithySettings_*` env-var mechanics.
- **Module 3** added the **recyclebin** — a registry table plus `CustomTableDrop` / `CustomTableRestore`
  hooks that turn an auto-drop into a recoverable set-aside instead of a destructive `DROP TABLE`.
- **Module 4** taught **promotion** — build one package, deploy the *same* artifact to dev, staging,
  and prod by changing nothing but `SmithySettings_ScriptTokens__TargetDb`.

This module runs all of it together, on a single package that carries the recyclebin from the start,
and puts a real mistake in front of it.

## Layout

```
course3-module-05/
  v1/<engine>/       known-good release: Customer + OrderHeader, FK'd, plus the recyclebin
  v2-bad/<engine>/   the same package, but OrderHeader renamed to SalesOrder the careless way
```

`<engine>` is `sqlserver`, `postgres`, `mysql`, or `mariadb`. Each `v1/` and `v2-bad/` carries its own
`Package/` plus a shared **`base.settings.json`** (connection details and package-path default). The
target database rides on the `{{TargetDb}}` script token, exactly as in Modules 2 and 4.

`v2-bad` renames `OrderHeader` to `SalesOrder` by simply changing the table name in the package — no
rename hint, nothing telling SchemaQuench "this is the same table, just renamed." SchemaQuench reads two
independent facts: a table it knows about (`OrderHeader`) is no longer in the product, and a table it's
never seen (`SalesOrder`) now is. Declaratively, that's indistinguishable from "drop one table, add
another" — because that's exactly what it is. `v1/<engine>/seed-prod.sql` seeds **prod only** with 3
customers and 4 orders, so there's real data on the line when the rename reaches it.

## Before you start

- The [sandbox](../docker) is up (`docker compose up -d`) and all four engines are healthy.
- The target databases exist. Run the Course 3 setup once (idempotent — safe to re-run):

  ```bash
  pwsh ../course3-setup/setup-environments.ps1     # or: ../course3-setup/setup-environments.sh
  ```

  It creates the twelve `ordersservice_{dev,staging,prod}` databases and reports `PASS` for each. This
  lab uses **all three** — dev, staging, and prod.
- The CLI is reachable. The commands below assume `schemaquench` is on your PATH
  (`schemaquench --version` reports `SchemaQuench - Version: 2.3.0.0`). If you'd rather not put it on
  the PATH, set `SCHEMAQUENCH` to the full path to the executable and substitute `$SCHEMAQUENCH` for
  `schemaquench` in every command below.

## The flow

Five beats, run in order. Pick an engine — the SQL Server form is fully worked below; the other three
engines follow with the same commands, just a different folder and a different `docker exec` client.

1. **Establish** — deploy `v1` to `ordersservice_dev`.
2. **Promote** — the SAME `v1` package to `ordersservice_staging`, then `ordersservice_prod` (cross-link
   Module 4). Seed prod with real orders.
3. **Ship the change** — deploy `v2-bad` to dev, then staging. Both succeed cleanly — there were no
   orders there to lose, so nothing looks wrong. This is the trap.
4. **The break reaches prod** — deploy `v2-bad` to prod. It succeeds with no error, but `OrderHeader`
   and its 4 orders are dropped — caught by the recyclebin — and `SalesOrder` is created empty.
5. **Roll back** — WhatIf `v1` against prod first (preview only), then apply it. SchemaQuench recreates
   `OrderHeader`, and the recyclebin's restore hook brings the data back automatically.

### SQL Server

```bash
cd v1/sqlserver        # steps 1-2 run from here

# 1. Establish: deploy v1 to dev
SmithySettings_ScriptTokens__TargetDb=ordersservice_dev \
  schemaquench --ConfigFile:./base.settings.json

# 2. Promote: the SAME v1 package to staging, then prod
SmithySettings_ScriptTokens__TargetDb=ordersservice_staging \
  schemaquench --ConfigFile:./base.settings.json
SmithySettings_ScriptTokens__TargetDb=ordersservice_prod \
  schemaquench --ConfigFile:./base.settings.json

# seed prod with real orders
docker exec -i learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -d ordersservice_prod \
  < seed-prod.sql
```

```bash
cd ../../v2-bad/sqlserver   # steps 3-4 run from here

# 3. Ship the change: v2-bad to dev, then staging — both look clean
SmithySettings_ScriptTokens__TargetDb=ordersservice_dev \
  schemaquench --ConfigFile:./base.settings.json
SmithySettings_ScriptTokens__TargetDb=ordersservice_staging \
  schemaquench --ConfigFile:./base.settings.json

# 4. The break reaches prod: OrderHeader (4 orders) is dropped, SalesOrder is created empty
SmithySettings_ScriptTokens__TargetDb=ordersservice_prod \
  schemaquench --ConfigFile:./base.settings.json
```

```bash
cd ../../v1/sqlserver   # step 5 runs from here — rolling back to the known-good package

# 5a. WhatIf first — preview the rollback, changes NOTHING
SmithySettings_ScriptTokens__TargetDb=ordersservice_prod \
SmithySettings_WhatIfONLY=true \
  schemaquench --ConfigFile:./base.settings.json

# 5b. Apply the rollback — OrderHeader is recreated and auto-restored with its 4 orders
SmithySettings_ScriptTokens__TargetDb=ordersservice_prod \
  schemaquench --ConfigFile:./base.settings.json
```

PowerShell equivalent (set the vars, run, then clear them):

```powershell
cd v1\sqlserver
$env:SmithySettings_ScriptTokens__TargetDb = 'ordersservice_dev'
schemaquench --ConfigFile:.\base.settings.json                                   # 1. establish (dev)
$env:SmithySettings_ScriptTokens__TargetDb = 'ordersservice_staging'
schemaquench --ConfigFile:.\base.settings.json                                   # 2a. promote (staging)
$env:SmithySettings_ScriptTokens__TargetDb = 'ordersservice_prod'
schemaquench --ConfigFile:.\base.settings.json                                   # 2b. promote (prod)
Get-Content .\seed-prod.sql | docker exec -i learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -d ordersservice_prod

cd ..\..\v2-bad\sqlserver
$env:SmithySettings_ScriptTokens__TargetDb = 'ordersservice_dev'
schemaquench --ConfigFile:.\base.settings.json                                   # 3a. ship (dev) — looks clean
$env:SmithySettings_ScriptTokens__TargetDb = 'ordersservice_staging'
schemaquench --ConfigFile:.\base.settings.json                                   # 3b. ship (staging) — looks clean
$env:SmithySettings_ScriptTokens__TargetDb = 'ordersservice_prod'
schemaquench --ConfigFile:.\base.settings.json                                   # 4. ship (prod) — OrderHeader dropped, SalesOrder empty

cd ..\..\v1\sqlserver
$env:SmithySettings_WhatIfONLY = 'true'
schemaquench --ConfigFile:.\base.settings.json                                   # 5a. WhatIf rollback (prod) — preview only
Remove-Item Env:SmithySettings_WhatIfONLY
schemaquench --ConfigFile:.\base.settings.json                                   # 5b. apply rollback (prod) — auto-restore
Remove-Item Env:SmithySettings_ScriptTokens__TargetDb
```

### PostgreSQL, MySQL, MariaDB

Same five beats, same commands — swap `sqlserver` for `postgres`, `mysql`, or `mariadb` in every `cd`,
and use that engine's client for the prod seed and any manual verification:

| Engine | Folder | Seed / verify client |
| ------ | ------ | --------------------- |
| PostgreSQL | `postgres` | `docker exec -i learn-postgres psql -U postgres -d ordersservice_prod` |
| MySQL | `mysql` | `docker exec -i learn-mysql mysql -uroot -pLearn!Passw0rd ordersservice_prod` |
| MariaDB | `mariadb` | `docker exec -i learn-mariadb mariadb -uroot -pLearn!Passw0rd ordersservice_prod` |

For example, seeding prod on PostgreSQL:

```bash
cd v1/postgres
docker exec -i learn-postgres psql -U postgres -d ordersservice_prod < seed-prod.sql
```

Everything else — the five beats, the env vars, the WhatIf-then-apply rollback — is identical across
all four engines. That's the whole point of the spine: one flow, every platform.

## What you'll see

**Steps 3 (dev, staging):** an ordinary clean deploy. `OrderHeader` drops, `SalesOrder` is created. No
warning that anything destructive happened, because in dev and staging `OrderHeader` was empty — there
was nothing to lose. This is exactly why the mistake ships: it passes everywhere it's tested.

**Step 4 (prod):** the same clean-looking deploy — SchemaQuench reports success, no error. But
`OrderHeader`, with its 4 orders, is gone from the schema, and `SalesOrder` exists with 0 rows. The
recyclebin's `CustomTableDrop` hook caught the drop on the way out, so the data isn't destroyed — it's
just not where the application expects it anymore. Verify both halves of that:

```bash
# SQL Server — SalesOrder is empty, OrderHeader shows up recycled in the registry
docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -d ordersservice_prod -h -1 -W \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.SalesOrder; SELECT OriginalName, RecycledName FROM recyclebin.Registry"

# PostgreSQL
docker exec learn-postgres psql -U postgres -d ordersservice_prod -tAc \
  "SELECT COUNT(*) FROM public.\"SalesOrder\"; SELECT original_name, recycled_name FROM recyclebin.registry"

# MySQL
docker exec -e MYSQL_PWD=Learn!Passw0rd learn-mysql mysql -uroot -N -e \
  "SELECT COUNT(*) FROM ordersservice_prod.SalesOrder; SELECT OriginalName, RecycledName FROM ordersservice_prod.recyclebin_Registry"

# MariaDB
docker exec -e MYSQL_PWD=Learn!Passw0rd learn-mariadb mariadb -uroot -N -e \
  "SELECT COUNT(*) FROM ordersservice_prod.SalesOrder; SELECT OriginalName, RecycledName FROM ordersservice_prod.recyclebin_Registry"
```

`SalesOrder` reads `0`. The registry lists `OrderHeader` as the recycled table — its 4 rows are sitting
there intact, just under a different name in a different schema. Dev and staging are green. Prod is
silently missing its order history. (This is the same "reading failures" discipline Course 8 covers in
depth — a success exit code doesn't mean the outcome was what you wanted; cross-link Module 3 for the
recyclebin mechanics themselves.)

**Step 5a (WhatIf):** the rollback preview. SchemaQuench diffs prod against `v1` and reports the
reverting delta — `SalesOrder` gets dropped (empty, nothing lost), `OrderHeader` gets created, and the
recyclebin's `CustomTableRestore` hook is the operation that will bring it back — without touching a row.

**Step 5b (apply):** the real rollback. SchemaQuench recreates `OrderHeader` — but instead of an empty
table, `CustomTableRestore` intercepts and restores the recycled copy: rows and all. Confirm the payoff:

```bash
# SQL Server
docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -d ordersservice_prod -h -1 -W \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.OrderHeader"

# PostgreSQL
docker exec learn-postgres psql -U postgres -d ordersservice_prod -tAc \
  'SELECT COUNT(*) FROM public."OrderHeader"'

# MySQL
docker exec -e MYSQL_PWD=Learn!Passw0rd learn-mysql mysql -uroot -N -e \
  "SELECT COUNT(*) FROM ordersservice_prod.OrderHeader"

# MariaDB
docker exec -e MYSQL_PWD=Learn!Passw0rd learn-mariadb mariadb -uroot -N -e \
  "SELECT COUNT(*) FROM ordersservice_prod.OrderHeader"
```

`OrderHeader` is back with all **4** rows, `SalesOrder` is gone, and prod matches the known-good `v1`
package again — data intact, no hand-written recovery script, no restore from backup.

## The principle

A declarative rename that isn't expressed as a rename isn't a rename at all — it's a drop and an add,
and SchemaQuench will do exactly that, faithfully, on every environment you point it at. Dev and staging
can't catch the damage because they had nothing to lose; only prod, where the data actually lives, shows
the cost. That's precisely why the recyclebin from Module 3 rides along in every package from the start:
it doesn't know or care whether a drop was intentional, so it catches all of them, careless renames
included. And because SchemaQuench's rollback is just "deploy the package that worked" (Module 3) with
the same promotion discipline that shipped the mistake (Module 4), recovering is the same tool, the same
command shape, no different from any other quench — WhatIf first, then apply, and the recyclebin's
restore hook does the rest.

## Teardown

Reset the databases for a clean re-run by re-running `setup-environments` — it's idempotent but won't
undo deployed state, so drop and recreate if you want a truly fresh start, or just re-deploy `v1` to
each environment to converge back to known-good.

After a cert pass, remove disposable build/run output before you're done — a cert pass isn't done until
disposable output is gone:

```bash
git clean -fdX
# or, more targeted:
rm -rf _certcli publish-* **/checkpoints
```
