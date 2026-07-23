# SmartObject-backed native sidebar

Use this pattern when every K2 Form remains a native Runtime page but must present one consistent application shell. The source of truth is a small SmartObject-backed List View on every Form. Style Profile JavaScript progressively enhances that native View into a responsive sidebar; there is no iframe or master page.

## Contents

- [Artifact set](#artifact-set)
- [Navigation contract](#navigation-contract)
- [SmartForms contract](#smartforms-contract)
- [Boot and anti-flash contract](#boot-and-anti-flash-contract)
- [Caching and reconciliation](#caching-and-reconciliation)
- [Implementation](#implementation)
- [Runtime validation](#runtime-validation)
- [Acceptance criteria](#acceptance-criteria)

## Artifact set

Copy `assets/examples/smartobject-sidebar` into the solution workspace. It contains:

- `navigation-contract.sql.template`: reusable SQL data contract;
- `smartforms-fragment.json`: required List View and Form composition;
- `sidebar-critical.css`: render-blocking anti-flash and native-source suppression;
- `sidebar-application.css`: full shell, navigation, responsive, focus, and transition styling;
- `sidebar-shell.js`: guarded boot coordinator, cache, reconciliation, routing, and transition behavior;
- `style-profile-manifest.json`: two critical K2 references plus separately hosted application CSS;
- `runtime-validation.json`: browser-test configuration;
- `build-assets.ps1`: deterministic minification.

Treat these as templates. Rename the `APP` prefix, K2 category, physical directory, virtual path, URLs, Forms, SmartObjects, and visual tokens. Keep the runtime/Designer guards, boot state machine, native-source fallback, and test assertions intact unless equivalent protection replaces them.

## Navigation contract

Expose a parameterless `List` method with properties in this exact order:

| Position | Property | Type | Purpose |
| --- | --- | --- | --- |
| 1 | `NavigationCode` | text | Stable unique key used by cache and tests. |
| 2 | `SectionLabel` | text, nullable | Optional visual group heading. |
| 3 | `Label` | text | User-facing link label. |
| 4 | `IconToken` | text, nullable | Allow-listed semantic icon token, not markup. |
| 5 | `TargetFormName` | text | K2 Runtime Form system name. |
| 6 | `SortOrder` | integer | Stable ascending order. |
| 7 | `IsActive` | boolean | Server-side enablement flag. |
| 8 | `ConfigurationVersion` | text | Increment when navigation rows or semantics change. |

The database should enforce a nonblank code, label, and target; unique code; nonnegative order; and deterministic ordering. Apply authorization in the data/service layer when links differ by user. Never rely on CSS or JavaScript to secure a destination.

## SmartForms contract

Create one native K2 List View over the navigation SmartObject. Include all eight properties in contract order, set its parameterless `List` method as the default list method, and place it first on every participating Form.

Set its Form View title to exactly `Application navigation`. The example CSS and JavaScript find the source using `[data-sf-title="Application navigation"]`; if the title changes, change that selector consistently in critical CSS, JavaScript configuration, and runtime validation.

Keep the View native and populated by K2 rules. JavaScript reads rendered rows and links but does not call SQL, K2 management APIs, or an unsecured custom endpoint. If enhancement fails, the native View remains the usable fallback after the fail-safe deadline.

## Boot and anti-flash contract

Use a two-tier asset model:

1. K2 loads small render-blocking critical CSS first.
2. K2 loads the small boot coordinator JavaScript second.
3. JavaScript requests versioned application CSS asynchronously.

Critical CSS must:

- apply only to Runtime with `html:not(.designer)`;
- cover the Runtime surface before enhanced layout becomes visible;
- suppress the native navigation source immediately;
- contain a CSS-only animation that reveals native K2 content after 2.5 seconds if JavaScript never runs.

JavaScript must:

- return before side effects when the URL or root class indicates Designer;
- create its state object and shell at most once;
- consider both application CSS and the required native K2 content before declaring readiness;
- reveal through two animation frames so layout and paint settle;
- clear the boot surface on success;
- fail open by the same bounded deadline if prerequisites never arrive.

Do not make the entire application stylesheet render-blocking. Do not remove the fail-open animation. A temporary DOM state before first contentful paint is not a visible flash; test actual painted exposure.

For Form-to-Form navigation, activate a lightweight transition curtain synchronously in the click handler, then navigate after approximately 80 ms. This gives the current page a paint opportunity before K2 performs the full document load. Honor reduced motion and never trap navigation if animation fails.

## Caching and reconciliation

Render version-matched navigation from `sessionStorage` immediately on warm loads. Then read the native SmartObject-backed rows and reconcile the shell. Use fallback rows only when neither cache nor live rows are available.

Store both rows and `ConfigurationVersion`. Discard malformed or version-mismatched cache content. A deployment that changes the application CSS or JavaScript should also change the hosted filename or URL version. A navigation data change should increment `ConfigurationVersion`.

Observe only long enough to acquire the native rows. Disconnect the `MutationObserver` immediately after successful reconciliation or the bounded retry period. Do not leave a full-document observer running for the lifetime of the Form.

Active-route highlighting is derived from the current Runtime Form URL, so direct links, browser history, and clicks inside Form content converge on the correct sidebar state after each page load.

## Implementation

1. Copy the example directory and replace placeholders.
2. Create the navigation table/view and SQL SmartObject with `$k2-sql-smartobjects`.
3. Merge the List View and Form fragment into the solution’s `$k2-smartforms` manifest.
4. Preserve the native View title and property order on every participating Form.
5. Set `window.K2SP_SIDEBAR_CONFIG` before the boot asset when default names are unsuitable.
6. Run `build-assets.ps1`; deploy only the generated `.min.css` and `.min.js` files.
7. Deploy the Style Profile with `k2style`; select it on Forms with `$k2-smartforms`.
8. Update `runtime-validation.json` and run the browser validation before handoff.

The Style Profile manifest uses `hosting.additionalFiles` for application CSS. It is hosted, guarded, hashed, and HTTP-verified, but is intentionally absent from K2’s ordered file list because the boot coordinator loads it asynchronously.

## Runtime validation

Run:

```powershell
& 'skills/k2-style-profiles/scripts/test-runtime.ps1' `
  -Config '.\runtime-validation.json' `
  -Output '.\runtime-validation-results.json'
```

The script launches a disposable authenticated Edge session and tests:

- asset status, MIME type, compression, and cache validators;
- direct asset injection into K2 Designer with no classes, shell, overlay, or state side effects;
- a cache-cleared cold load with one ready shell, hidden native source, no overflow, and no native-source exposure after first contentful paint;
- a warm Form transition with a curtain activated in under 100 ms, active-route reconciliation, and versioned navigation cache;
- a blocked boot script, proving the CSS-only timeout reveals usable native SmartForms content;
- unexpected browser exceptions and log errors.

The output JSON preserves checks, paint samples, navigation timings, state snapshots, asset headers, and diagnostics. Run `-SelfTest` to validate a configuration without launching Edge or contacting K2.

## Acceptance criteria

- Every participating Form has exactly one enhanced shell and one native navigation source.
- Cold and warm scenarios show no post-paint native navigation flash.
- The transition curtain activates synchronously before full-page navigation.
- Direct links and browser history select the correct route.
- Cache version and live SmartObject rows reconcile without duplicate links.
- JavaScript failure exposes usable native content within the declared deadline.
- K2 Designer remains entirely inert.
- CSS/JS are minified, served compressed, and provide cache headers or validators.
- Keyboard focus, visible focus, reduced motion, responsive sidebar/bottom navigation, native validation, and K2 task actions remain usable.
