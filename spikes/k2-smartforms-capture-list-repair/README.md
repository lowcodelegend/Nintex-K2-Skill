# Capture-list in-place repair probe

This disposable integration probe proves that `k2forms repair-view` preserves
a live editable-list View's identity and dependency contract.

1. Deploy `baseline-manifest.json`. It creates one eight-column editable-list
   View and one consuming Form under the dedicated `Generator Probes`
   category.
2. Inspect the baseline and record its live View GUID. Confirm that the View
   is checked in and has no `ShowAddRow` property.
3. Run `repair-view` with `repair-manifest.json`, the recorded GUID, and a new
   backup path. The repair hides four technical columns.
4. Verify from a fresh process. The View must retain its GUID, exact
   name/display name, category, primary SmartObject binding and Form
   dependency; it must be checked in with four aligned Header, Display,
   Footer and Edit cells and no `ShowAddRow` property.
5. Run manifest-only cleanup against `repair-manifest.json` to delete only the
   disposable Form and View.

Never substitute a production View for this proof. Do not use replacement,
Designer automation or direct K2 database editing.
