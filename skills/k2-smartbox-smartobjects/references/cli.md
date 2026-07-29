# k2smartbox CLI

All commands load and validate the manifest before connecting to K2.

- `doctor` checks the local K2 installation, K2 connection, and installed SmartBox Service Instance.
- `plan` is read-only and reports create, additive update, or collision decisions.
- `deploy --confirm` publishes exact manifest objects, places them in `<root>\Data`, and verifies them.
- `verify` checks the SmartBox service binding, property contract, standard methods, category placement, and optional List smoke tests.
- `inspect` prints live GUIDs, properties, methods, and category paths.
- `cleanup --confirm` deletes only exact manifest-owned SmartObjects. `--delete-root-category` also attempts the root after deleting `Data`; non-empty categories are retained.
- `version` and `selftest` do not require a manifest.

Mutating commands refuse to run without `--confirm`. Set `K2SMARTBOX_DEBUG=1` for exception details.

If deployment stops partway through, inspect the live objects, correct the manifest, and rerun `plan`. Creation is idempotent by system name. Existing objects are changed only when `deployment.updateExisting` is true and the change is additive.
