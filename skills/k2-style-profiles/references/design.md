# Style Profile design

## Loading model

K2 loads a Style Profile's external files in declared order. Treat that list as a dependency graph:

1. tokens, fonts, reset, and native-control normalization;
2. layout, shell, navigation, forms, tables, dashboards, and responsive styles;
3. runtime behavior and DOM enhancement JavaScript.

Keep assets few and small. Minify production files before deployment, enable IIS static compression, cache immutable versioned filenames, and avoid font families that require blocking cross-origin downloads.

For an application shell, split delivery into render-blocking critical CSS, a small boot coordinator, and asynchronously loaded application CSS. Critical CSS owns the initial cover, immediate native-source suppression, and CSS-only fail-open deadline. The coordinator owns readiness, cache reconciliation, and the two-frame reveal. See [smartobject-sidebar.md](smartobject-sidebar.md).

## Runtime/Designer boundary

Style Profile files can be requested while a Form or Style Profile is open in Designer. Guard at both layers:

- Prefix CSS selectors with `html:not(.designer)` or an equivalent root selector proven on the target K2 version.
- Make JavaScript determine Designer mode and return before adding classes, observers, overlays, loading screens, navigation, or event listeners.
- Keep the marker `/* k2style: designer-guard */` beside that early return so validation and reviewers can locate it.
- Re-test after K2 upgrades because root classes and routes are implementation details.

Never use a delayed cleanup as the primary guard. A loading overlay that appears briefly in Designer is still a defect.

## Loading and transition quality

- Gate the reveal on application CSS plus the native K2 content the enhancement requires.
- Reveal after two `requestAnimationFrame` callbacks so the computed layout reaches a paint boundary.
- Keep a bounded CSS-only fail-open path if JavaScript is blocked before it can initialize.
- Render version-matched cached navigation first, then reconcile native SmartObject rows.
- Hide the native source in critical CSS, not only after JavaScript finds it.
- Activate a transition curtain synchronously during navigation so it paints before the next full K2 Form load.
- Measure cold load, warm load, Form transition, and failure path. Treat DOM visibility before first contentful paint as unpainted state, not a user-visible flash.

## DOM enhancement

K2 Runtime performs partial updates and can replace nodes. DOM manipulation must:

- detect existing enhancements and remain idempotent;
- prefer stable names, semantic attributes, and configured hooks over generated GUID selectors;
- observe the smallest stable container and batch mutations;
- preserve native inputs, labels, validation, keyboard behavior, focus, and task actions;
- fail open—native SmartForms must remain usable if JavaScript fails;
- honor `prefers-reduced-motion`;
- avoid hiding content until enhancement succeeds.

## Integration with SmartForms

Create and verify the Style Profile first. In a `$k2-smartforms` manifest, set `application.styleProfile` to its exact name or GUID and keep `useLegacyTheme=false`. Style Profile deployment does not edit existing Forms; changing a Form's selected profile remains SmartForms work.

For shared navigation or shell behavior, keep the data and server rules native where possible. Use Style Profile JavaScript to progressively enhance stable native markup, not to replace authorization, routing, persistence, or workflow logic.

Choose the navigation source deliberately:

- Use a SmartObject-backed List View for cross-Form application routes; see [smartobject-sidebar.md](smartobject-sidebar.md).
- Move the real K2 Form tab strip for sections within one Form; see [native-tabs-sidebar.md](native-tabs-sidebar.md).

Never clone native tab anchors into a second menu. Moving the original node preserves K2 click handlers, IDs, rule-driven selection, tab panels, and Worklist behavior.

## Verification checklist

- New profile has a unique GUID, expected category, version, and checked-in state.
- File type, URL, and order exactly match the manifest.
- Every URL returns 2xx over HTTPS with the expected MIME type.
- Served bytes equal source bytes.
- Runtime works with empty, long, invalid, slow, mobile, keyboard-only, and reduced-motion states.
- Designer opens without runtime classes, overlays, observers, sidebar transforms, or loading screens.
- Existing forms using other Style Profiles are unchanged.
