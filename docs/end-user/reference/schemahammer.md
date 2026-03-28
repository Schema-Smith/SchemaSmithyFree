# SchemaHammer Reference

SchemaHammer is a read-only, cross-platform desktop viewer for browsing SchemaSmith schema packages. It gives you a visual, hierarchical view of everything in a product -- templates, tables, indexed views, migration scripts, and script tokens -- without connecting to a server or modifying any files. Use SchemaHammer whenever you need to understand what a schema package contains, inspect object definitions, trace token values, or review migration scripts.

---

## Installation and Invocation

SchemaHammer is included in the SchemaSmith distribution. Launch it from the command line:

```bash
SchemaHammer
```

You can also pass the path to a product directory directly:

```bash
SchemaHammer path/to/product
```

When launched without arguments, SchemaHammer opens the welcome screen. If you previously had a product open, it automatically reloads that product and restores your last-selected node.

---

## Opening a Product

### File Picker

Go to **File > Choose Product** and select the `Product.json` file for the product you want to browse. SchemaHammer loads the full product tree in the left panel.

### Recent Products

**File > Recent Products** lists up to 10 recently opened products, most recent first. Select any entry to open that product immediately without navigating to its folder. The list is persisted across sessions.

### Auto-Reload on Launch

When SchemaHammer starts, it checks whether the most recent product still exists on disk. If so, it loads it automatically, restoring you to exactly where you left off.

---

## Product Tree

The left panel displays the schema package as a hierarchical tree:

- **Product** -- The root node. Selecting it opens the product editor.
  - **Templates** -- One node per template in the product.
    - **Tables** -- Table definitions belonging to the template.
      - Columns, Indexes, Foreign Keys, Check Constraints, Statistics, XML Indexes, Full-Text Indexes
    - **Scripts** -- Migration script folders and individual script files.
    - **Indexed Views** -- Indexed view definitions, structured similarly to tables.
      - Columns, Indexes, Statistics

### Lazy Loading

Tables and indexed views do not load their child nodes (columns, indexes, foreign keys, etc.) until you expand them. This keeps the tree responsive when opening large products with hundreds of tables. Once expanded, children remain loaded for the rest of the session.

### Selecting Nodes

Click any node to select it. The right panel immediately displays the corresponding editor with all properties for that object.

---

## Navigation

### Back Button and History

The toolbar includes a **Back** button that returns to the previously selected node. Click the dropdown arrow next to the Back button to see your full navigation history for the current session. Select any entry to jump directly to that node.

### Last Selection Restored

When you reopen a product, SchemaHammer restores the exact node you had selected when you last closed it. This saves time when returning to a specific part of a large schema.

### Reloading the Product

If you modify schema package files on disk while SchemaHammer is open, press **F5** or go to **File > Reload Tree** to refresh the product from disk. The tree reloads and attempts to restore your current selection.

---

## Property Editors

Each node type opens a dedicated read-only editor in the right panel. Every field listed below is displayed when you select a node of that type.

### Product

| Field | Description |
|-------|-------------|
| Name | Product name |
| Platform | Target platform (e.g., SqlServer) |
| Minimum Version | Minimum SQL Server version required |
| Drop Unknown Indexes | Whether indexes not in the product are dropped during deployment |
| Validation Script | Script executed to validate target databases |
| Baseline Validation Script | Script executed during baseline validation |
| Version Stamp Script | Script used for version stamping |
| Template Order | Ordered list of template names |
| Script Tokens (tab) | Key-value pairs for product-level script tokens |

### Template

| Field | Description |
|-------|-------------|
| Name | Template name |
| Database Identification Script | Script that identifies which databases this template applies to |
| Version Stamp Script | Script used for version stamping |
| Baseline Validation Script | Script executed during baseline validation |
| Update Fill Factor | Whether fill factor is actively managed |
| Script Tokens (tab) | Key-value pairs for template-level script tokens |

### Table

| Field | Description |
|-------|-------------|
| Schema and Name | Displayed in the editor title as `schema.name` |
| Compression Type | Data compression setting (NONE, ROW, PAGE) |
| Is Temporal | Whether this is a temporal (system-versioned) table |
| Update Fill Factor | Whether fill factor is actively managed |
| Old Name | Previous name, used by SchemaQuench for rename tracking |

### Column

| Field | Description |
|-------|-------------|
| Name | Column name |
| Data Type | SQL Server data type |
| Nullable | Whether the column allows nulls |
| Default | Default value expression |
| Check Expression | Column-level check constraint expression |
| Computed Expression | Computed column expression |
| Persisted | Whether the computed column is persisted |
| Sparse | Whether the column uses sparse storage |
| Collation | Column collation override |
| Data Mask Function | Dynamic data masking function |
| Old Name | Previous name, used for rename tracking |

### Index

| Field | Description |
|-------|-------------|
| Name | Index name |
| Compression Type | Data compression setting |
| Primary Key | Whether this index backs a primary key constraint |
| Unique | Whether the index enforces uniqueness |
| Unique Constraint | Whether this is a unique constraint rather than a unique index |
| Clustered | Whether the index is clustered |
| Column Store | Whether this is a columnstore index |
| Fill Factor | Index fill factor percentage |
| Index Columns | Comma-separated list of key columns |
| Include Columns | Comma-separated list of included (non-key) columns |
| Filter Expression | Filter predicate for filtered indexes |
| Update Fill Factor | Whether fill factor is actively managed |

### Foreign Key

| Field | Description |
|-------|-------------|
| Name | Foreign key name |
| Columns | Local columns participating in the relationship |
| Related Table Schema | Schema of the referenced table |
| Related Table | Name of the referenced table |
| Related Columns | Columns in the referenced table |
| Delete Action | Referential action on delete (NO ACTION, CASCADE, SET NULL, SET DEFAULT) |
| Update Action | Referential action on update |

### Check Constraint

| Field | Description |
|-------|-------------|
| Name | Constraint name |
| Expression | Boolean expression that must evaluate to true |

### Statistic

| Field | Description |
|-------|-------------|
| Name | Statistic name |
| Columns | Columns covered by the statistic |
| Sample Size | Sampling percentage |
| Filter Expression | Filter predicate for filtered statistics |

### XML Index

| Field | Description |
|-------|-------------|
| Name | XML index name |
| Is Primary | Whether this is a primary XML index |
| Column | XML column the index is built on |
| Primary Index | Name of the associated primary XML index (for secondary indexes) |
| Secondary Index Type | Type of secondary XML index (PATH, VALUE, PROPERTY) |

### Full-Text Index

| Field | Description |
|-------|-------------|
| Full Text Catalog | Catalog that owns the index |
| Key Index | Unique index used as the full-text key |
| Change Tracking | Change tracking mode |
| Stop List | Stop list used for noise word filtering |
| Columns | Columns included in the full-text index |

### Indexed View

| Field | Description |
|-------|-------------|
| Schema | View schema |
| Name | View name |
| Definition | The view's SQL definition |
| Index Summary | List of indexes defined on the view |

Expand an indexed view node in the tree to browse its child objects -- columns, indexes, and statistics -- each with their own editors as described above.

---

## SQL Script Viewer

Selecting a script file opens the SQL viewer with T-SQL syntax highlighting. The viewer is read-only.

### Token Preview

Scripts often contain `{{{TokenName}}}` placeholders. Click the **Preview** button in the toolbar to toggle between two modes:

- **Raw mode** (default) -- Shows the script text exactly as stored on disk, with `{{{...}}}` placeholders visible.
- **Preview mode** -- Expands every placeholder to its resolved value. Product-level tokens are applied first, then template-level tokens override any matching keys.

The button label switches between "Preview" and "Raw" to indicate the current state.

---

## Token Navigation

In any SQL script, `{{{TokenName}}}` placeholders appear as clickable links. Double-click a token to navigate directly to its definition:

1. SchemaHammer checks the parent template's script tokens first.
2. If the token is not defined at the template level, it falls back to the product's script tokens.
3. The corresponding Product or Template editor opens with the **Script Tokens** tab selected and the matching token highlighted.

This makes it fast to trace where a value comes from when reading migration scripts.

---

## Tree Search

Tree search filters the product tree by object name.

**Open:** Press **Ctrl+F** (when the SQL editor is not focused) or go to **Search > Search Tree**.

| Setting | Behavior |
|---------|----------|
| **Contains** | Matches nodes whose name includes the search term anywhere (default) |
| **Begins With** | Matches nodes whose name starts with the search term |
| **Ends With** | Matches nodes whose name ends with the search term |

- **Auto-search** -- Results update automatically as you type after a short debounce pause.
- **Filtered results** -- Container nodes (folder headings like "Tables" or "Scripts") are excluded. Only navigable objects appear.
- **Navigate** -- Double-click any result to select that node in the tree and open its editor.

---

## Code Search

Code search looks inside the content of your schema package rather than just object names.

**Open:** Press **Ctrl+Shift+F** or go to **Search > Search Code**.

### What Gets Searched

- **SQL script files** -- Full text content of every script in the product.
- **Table metadata** -- Table names, column names, column defaults, computed expressions, check expressions, index names, index columns, include columns, filter expressions, foreign key names and columns, check constraint names and expressions, and statistic names, columns, and filter expressions.
- **Script token key/value pairs** -- Both product-level and template-level tokens, matching on either the key or the value.

### Using Code Search

- Code search does **not** auto-search. Type your term and press **Enter** or click **Search** to execute.
- Double-click any result to navigate to the matching node in the tree.
- For script token results, SchemaHammer opens the Product or Template editor with the **Script Tokens** tab selected and the matching token highlighted.

---

## Find Bar (In-Editor Search)

When a SQL script is open and focused, press **Ctrl+F** to open the find bar at the bottom of the editor. This searches within the current script only.

| Control | Behavior |
|---------|----------|
| **Search field** | Type to search; match count updates as you type |
| **F3** / **Next** | Move to the next match |
| **Shift+F3** / **Previous** | Move to the previous match |
| **Match case toggle** | Switch between case-sensitive and case-insensitive matching |
| **Match count** | Displays current position and total (e.g., "3 of 12") |
| **Escape** | Close the find bar |

The find bar is read-only -- there is no replace function.

---

## Update Schema Files

**Tools > Update Schema Files** regenerates the `.json-schemas/` validation files for the current product. These files provide JSON schema validation in editors such as Visual Studio Code when you edit `Product.json`, `Template.json`, and table JSON files directly. Run this after upgrading SchemaSmith to pick up any changes to the schema format.

---

## Themes

SchemaHammer ships with light and dark themes. Toggle between them via **View > Toggle Theme**. Your preference is saved and restored on the next launch.

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| **F5** | Reload the product tree from disk |
| **Ctrl+F** | Open tree search (or in-editor find bar when a SQL script is focused) |
| **Ctrl+Shift+F** | Open code search |
| **F3** | Next match in find bar |
| **Shift+F3** | Previous match in find bar |
| **Escape** | Close the active dialog, search panel, or find bar |

---

## About Dialog

**Help > About** displays the current SchemaHammer version number and a link to the SchemaSmith Community GitHub repository.
