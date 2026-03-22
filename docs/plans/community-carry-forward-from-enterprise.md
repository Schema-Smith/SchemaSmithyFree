# Community Carry-Forward from Enterprise (SchemaForge)

**Created:** 2026-03-22
**Purpose:** SQL Server bug fixes and features implemented in SchemaForge (Enterprise) during Spec 4 work that need to be ported to SchemaSmithyFree (Community). Excludes multi-platform features, data delivery special type support, and Demo edition changes which don't apply to Community.

---

## 1. Indexed View Procedures Missing from KindleTheForge — BUG FIX

**Priority:** High (P0 — indexed views completely broken without this)
**Risk:** Without these procedures deployed, any indexed view quench fails with "object not found" at runtime.

**What was fixed:**
`KindleForSqlServer()` was not deploying 4 indexed view stored procedures. They were created as SQL scripts but never registered in the kindling sequence.

**Enterprise source:**
- `Schema/Utility/ForgeKindler.cs` — `KindleForSqlServer()` method (lines 39-59). Lines 55-58 add the 4 procedures.
- Unit test: `Schema/Schema.UnitTests/Utility/ForgeKindlerTests.cs` — validates all procedures are registered.

**Procedures to register:**
1. `SchemaSmith.ValidateIndexedViewOwnership`
2. `SchemaSmith.FixupIndexedViewOwnership`
3. `SchemaSmith.IndexedViewQuench`
4. `SchemaSmith.GenerateIndexedViewJson`

---

## 2. Cross-Product Ownership Validation for Indexed Views — BUG FIX

**Priority:** High (P1 — data corruption risk)
**Risk:** Two products claiming the same indexed view would silently fail instead of throwing a clear ownership conflict error.

**What was fixed:**
Added explicit ownership conflict detection in `IndexedViewQuench.sql`. If a view's `SchemaSmith_Product` extended property names a different product, the quench now throws error 50001 with a descriptive message instead of failing with a cryptic CREATE VIEW error.

**Enterprise source:**
- `Schema/Scripts/SqlServer/SchemaSmith.IndexedViewQuench.sql` — lines 32-49 (ownership validation block using `STRING_AGG` to build conflict message)

---

## 3. FillFactor Read from Wrong JSON Level — BUG FIX

**Priority:** High (P1 — performance degradation, unnecessary index rebuilds)
**Risk:** All indexes use the table-level FillFactor (default 100) instead of the index-specific value. Causes spurious `ALTER INDEX SET (fillfactor)` on every re-quench.

**What was fixed:**
`ParseTableJsonIntoTempTables.sql` and `IndexOnlyQuench.sql` were reading `FillFactor` and `UpdateFillFactor` from the table-level JSON element instead of the index-level JSON element.

**Enterprise source:**
- `Schema/Scripts/SqlServer/ParseTableJsonIntoTempTables.sql` — line 101: FillFactor extraction now reads from index element `i.[FillFactor]`
- `Schema/Scripts/SqlServer/SchemaSmith.IndexOnlyQuench.sql` — line 37: same fix

**How to verify:** Compare the FillFactor extraction in Community's `ParseTableJsonIntoTempTables.sql` and `IndexOnlyQuench.sql` against Enterprise. The JSON path should reference the index-level element, not the table-level element.

---

## 4. Indexed View Integration Test Suite — NEW TESTS

**Priority:** High (test infrastructure)

**Enterprise source:**
- `SchemaQuench/SchemaQuench.IntegrationTests/SqlServer/IndexedViewQuenchTests.cs` — 9 tests:
  1. `InitialQuench_CreatesViewWithClusteredAndNonclusteredIndexes`
  2. `ReQuench_WithNoChanges_ViewStillExists`
  3. `ReQuench_WithNoChanges_DoesNotRebuildView`
  4. `IndexOnlyChange_DoesNotRebuildView`
  5. `DefinitionChange_TriggersRebuild`
  6. `EmptyArray_DropsView`
  7. `IndexRemoval_RemovesIndex`
  8. `OwnershipFixup_ReassignsProduct`
  9. `MultipleViews_QuenchedIndependently`

**Notes:**
- Class is `[NonParallelizable]` — tests share a database within the fixture
- Each test is self-contained (no `[Order]` dependencies)
- Uses `EnsureViewExists`/`EnsureViewDropped` helpers for preconditions
- Creates its own isolated database (`IvTest_<timestamp>_<guid>`)

---

## 5. Indexed View Extraction Tests — NEW TESTS

**Priority:** High (test infrastructure)

**Enterprise source:**
- `SchemaTongs/SchemaTongs.IntegrationTests/SqlServer/GenerateIndexedViewJsonTests.cs` — 3 tests:
  1. `ShouldGenerateCorrectJsonForIndexedView` — validates JSON structure, Schema, Name, Definition (SELECT only), Indexes array, clustered index ordering
  2. `ShouldHandleIndexedViewWithNoNonclusteredIndexes` — single clustered index only
  3. `ShouldHandleMultipleIndexedViews` — two views in one extraction

---

## 6. Test Product Enhanced with Indexed Views — TEST DATA

**Priority:** Medium (enables regression testing through product quench)

**Enterprise source:**
- `TestProducts/SqlServer/ValidProduct/Templates/Main/Indexed Views/dbo.vTestSummary.json` — sample indexed view definition added to the ValidProduct test product

**Impact:** `ProductQuenchTests.ShouldQuenchValidProductSuccessfully()` now exercises the full indexed view quench flow.

---

## Summary

| # | Item | Type | Priority | Enterprise Source |
|---|------|------|----------|-------------------|
| 1 | Indexed view procs missing from KindleTheForge | Bug fix | High | `ForgeKindler.cs:55-58` |
| 2 | Cross-product ownership validation | Bug fix | High | `IndexedViewQuench.sql:32-49` |
| 3 | FillFactor wrong JSON level | Bug fix | High | `ParseTableJsonIntoTempTables.sql:101`, `IndexOnlyQuench.sql:37` |
| 4 | Indexed view quench test suite | Tests | High | `IndexedViewQuenchTests.cs` (9 tests) |
| 5 | Indexed view extraction tests | Tests | High | `GenerateIndexedViewJsonTests.cs` (3 tests) |
| 6 | ValidProduct indexed view test data | Test data | Medium | `Indexed Views/dbo.vTestSummary.json` |
