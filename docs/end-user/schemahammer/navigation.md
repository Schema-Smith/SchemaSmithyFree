# Navigation

Applies to: SchemaHammer (SQL Server, Community)

---

## The Product Tree

The left panel shows the full structure of the open schema package as a tree:

- **Product** — The root node. Select it to view product-level properties and script tokens.
  - **Templates** — Each template in the product.
    - **Tables** — Table definitions under the template. Tables and indexed views load their children (columns, indexes, foreign keys, constraints, statistics) when you expand them.
    - **Scripts** — Migration script folders and individual script files.
    - **Indexed Views** — Indexed view definitions, structured like tables.

Selecting any node opens its details in the right panel.

---

## Lazy Loading

Tables and indexed views do not load their child nodes until you expand them. This keeps the tree fast when opening large products. Once expanded, the children remain loaded for the rest of the session.

---

## Back Button and History

The toolbar includes a **Back** button that navigates to the previously selected node. Click the arrow next to the Back button to open a dropdown showing your full navigation history for the current session. Select any entry in the list to jump directly to that node.

---

## Last Selection Restored

When you reopen a product, SchemaHammer restores the node you had selected when you last closed it. This saves time when returning to work on a specific part of the schema.

---

## Recent Products

**File > Recent Products** lists up to 10 recently opened products. Select an entry to open that product immediately without navigating to its folder.

---

## Reloading the Product

If you modify the schema package files on disk while SchemaHammer is open, use **F5** or **File > Reload Tree** to refresh the product from disk. The tree reloads and attempts to restore your current selection.

---

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| F5 | Reload the product tree from disk |
| Ctrl+F | Open tree search (or the in-editor find bar when a SQL script is focused) |
| Ctrl+Shift+F | Open code search |
| Escape | Close the active dialog or search panel |
