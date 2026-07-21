# EXP.Expense Claims

A disposable complete-solution example for the K2 master-detail contract. It creates a SQL-backed expense-claim header, line-item table, lookup table, summary view, generated SmartObjects, a four-column capture/item master View with explicit defaults for read-only required Create inputs, an editable-list detail View with a category dropdown, and a tabbed Form whose single Form-level Save action transfers the header identity, persists line states, and reloads only that header's lines.

Deploy data first, then forms. The fixture is designed to be removed after live verification.
