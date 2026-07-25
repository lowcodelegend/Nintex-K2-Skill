# Testing and evidence

## Static gates

Validate JSON syntax, tag uniqueness, resource existence, duplicate filenames, script/style order, exactly one optional `listdata`, standard property implementations, event/method implementations, scoped globals/CSS, and the absence of every legacy artifact.

Reject ZIP traversal paths, symlinks, hidden secrets, source maps containing source secrets, remote executable scripts, and undeclared files.

## Standalone and Designer gates

Run the component in a browser harness with the K2 Web Component shims. Exercise design-time and runtime independently. The design-time class must render without network/SmartObject calls and react to property changes.

After registration:

- Refresh Designer and confirm the control appears under Custom.
- Place it, save, close, reopen, and verify Designer hydration without an infinite spinner or console error.
- Verify every property, event, method, data-binding mapping, and standard state in the Designer.
- Inspect the saved definition and confirm the expected registered control type and stable property IDs.

## Runtime gates

Use an authenticated K2 Runtime session and capture:

- desktop 1440×1000;
- laptop 1280×800;
- tablet 768×1024;
- mobile 390×844;
- 200% browser zoom where practical.

Reject document/control horizontal overflow, clipping, overlap, unreadable long content, missing focus, inaccessible dialogs, duplicate IDs, unexpected console/page errors, or silent network failures. Exercise populated, empty, loading, error, read-only, disabled, long-content, and reduced-motion states.

For input controls, prove that K2 Form/View validation invokes the component `Validate()` contract and that invalid values never reach persistence. For dashboard controls, prove list binding, refresh, empty data, stale data, and malformed-row handling.

## Northstar fidelity gate

The prototype at `examples/supplier-nonconformance/gold-standard-prototype` is the visual source of truth for the canonical case homepage. Compare the Web Component harness and authenticated K2 Runtime against:

- `review/command-desktop.png` at 1440×1000;
- the prototype's mobile shell at 390×844;
- interaction states for command search, mobile navigation, focus, notifications, and new-case navigation.

Use the same browser build, viewport, device scale, fonts, and deterministic fixture data. Record pixel mismatch ratio plus perceptual diff. Require no structural mismatch and no unexplained visible pixel region. Font/network variance is a failed dependency, not an automatic waiver.

Also verify semantics: one H1, landmark navigation/main, skip link, chart text alternative, colour-independent status, correct tab order, Escape-close dialog, Ctrl/Cmd+K search, and screen-reader announcements.
