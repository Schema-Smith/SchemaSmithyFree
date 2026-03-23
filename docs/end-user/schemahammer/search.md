# Searching

Applies to: SchemaHammer (SQL Server, Community)

---

## Tree Search

Tree search filters the product tree by object name, letting you quickly locate a table, script, or other node by typing part of its name.

**To open tree search:** Press **Ctrl+F** while the SQL editor is not focused, or go to **Search > Search Tree**.

- **Match mode** — Choose from Contains, Begins With, or Ends With.
- **Auto-search** — Results update automatically as you type. There is a short pause after each keystroke before the search runs.
- **Results** — Only navigable objects are shown. Container nodes (such as "Tables" or "Scripts" folder headings) are filtered out.
- **Navigate to a result** — Double-click any result to select that node in the tree and open its editor.

---

## Code Search

Code search looks inside the content of your schema package — across SQL script files, table definitions, and script token values — rather than just object names.

**To open code search:** Press **Ctrl+Shift+F**, or go to **Search > Search Code**.

- **What is searched** — SQL script file contents, table metadata (columns, indexes, foreign keys, constraints, and statistics), and script token key/value pairs.
- **Manual trigger** — Code search does not run automatically as you type. Click **Search** or press **Enter** to run the search.
- **Navigate to a result** — Double-click any result to navigate to the corresponding node in the tree. For script token results, SchemaHammer navigates to the Script Tokens tab on the relevant Product or Template editor.

---

## In-Editor Find Bar

When a SQL script is open and focused, pressing **Ctrl+F** opens the find bar at the bottom of the editor. This searches within the text of the current script only.

- **Navigation** — Press **F3** to move to the next match, **Shift+F3** to move to the previous match.
- **Match case** — Toggle case-sensitive matching with the match case button in the find bar.
- **Match count** — The find bar shows the current position and total number of matches (for example, "3 of 12").
- **Read-only** — The find bar supports finding text only. There is no replace function.
- **Close** — Press **Escape** to close the find bar and return focus to the editor.

---

## Token Navigation

In any SQL script viewer, `{{{TokenName}}}` placeholders appear as clickable links. Double-clicking a token navigates directly to its definition on the Script Tokens tab of the Product or Template editor where that token is defined.
