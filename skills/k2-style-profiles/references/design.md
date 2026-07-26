# Style Profile design

## Loading model

K2 loads a Style Profile's external files in declared order. Treat that list as a dependency graph:

1. tokens, fonts, reset, and native-control normalization;
2. layout, shell, navigation, forms, tables, dashboards, and responsive styles;
3. runtime behavior and DOM enhancement JavaScript.

Keep assets few and small. Minify production files before deployment, enable IIS static compression, cache immutable versioned filenames, and avoid font families that require blocking cross-origin downloads.

For a colour scheme, do not begin with button and header selectors. Generate a complete adapter from the target server's installed K2 dynamic variable contract as described in [color-schemes.md](color-schemes.md), then add narrowly scoped polish only where the platform exposes no variable.

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

Create and verify the Style Profile first. In a `$k2-smartforms` manifest, set `application.styleProfile` to its exact name or GUID, keep `useLegacyTheme=false`, and set `form.useStyleProfile=false` on Forms that should retain K2's plain modern default. Style Profile deployment does not edit existing Forms; changing or removing a Form's selected profile remains SmartForms reconciliation/regeneration work.

For shared navigation or shell behavior, keep the data and server rules native where possible. Use Style Profile JavaScript to progressively enhance stable native markup, not to replace authorization, routing, persistence, or workflow logic.

Choose the navigation source deliberately:

- Use a SmartObject-backed List View for cross-Form application routes; see [smartobject-sidebar.md](smartobject-sidebar.md).
- Move the real K2 Form tab strip for sections within one Form; see [native-tabs-sidebar.md](native-tabs-sidebar.md).

Never clone native tab anchors into a second menu. Moving the original node preserves K2 click handlers, IDs, rule-driven selection, tab panels, and Worklist behavior.

When a guided Form owns the command-palette View on its first screen, advancing the native tab causes K2 to hide the containing `.formpanel`. Keep the live control in that ownership tree. Mark the palette's stable row and its closest native panel, then use a later, narrowly scoped CSS override to reduce a hidden palette panel to a zero-size, overflow-visible surface. Hide every sibling row and restore pointer events only on the palette row. This keeps one Designer-hydratable control and its View-owned rule available on every screen without revealing stale first-screen content.

Completed-step indicators must not depend on a text glyph's font metrics. Remove the step number from layout with `font-size: 0`, make the circle a centered flex container, and draw the check as an absolutely centered, fixed-size bordered pseudo-element. Verify its computed position and dimensions at desktop and mobile widths.

## Validation presentation

Native K2 validation remains authoritative. A Style Profile that changes an input border, background, or shadow must explicitly cover invalid TextBox/TextArea and memo wrappers, dropdown/select-box buttons, calendars, checkboxes, and File controls. Put the invalid contract after the neutral contract with equal or greater selector specificity. Important neutral declarations require important invalid declarations; manifest loading, `doctor`, and every mutating command reject important overrides without that later protection.

For a guided journey, the Northstar shell adds only feedback around native validation: one live summary for the active screen, the native invalid count, `aria-invalid`/`aria-describedby`, and focus plus scroll to the first failure. It observes K2's classes and removes or updates the summary as K2 clears them. It does not decide validity, navigate, persist, or replace validation messages.

Some K2 5.10 Runtime builds reference the misspelled localization global `locValidationExpresssionsFailed` without declaring it. Probe after the Runtime/Designer guard. Supply a narrowly scoped default only when `typeof window.locValidationExpresssionsFailed === "undefined"`; preserve every existing value and never patch installed K2 JavaScript.

## Verification checklist

- New profile has a unique GUID, expected category, version, and checked-in state.
- File type, URL, and order exactly match the manifest.
- Every URL returns 2xx over HTTPS with the expected MIME type.
- Served bytes equal source bytes.
- Runtime works with empty, long, invalid, slow, mobile, keyboard-only, and reduced-motion states.
- Clicking an empty guided Continue/Save action invokes its real K2 event, blocks navigation/persistence, visibly treats every expected invalid control, exposes the matching summary count, and focuses/reveals the first failure without browser exceptions or horizontal overflow.
- After real Pre-fill/Continue/Save navigation, the original command palette remains visible, opens, focuses its search input, and returns governed options on every guided screen.
- Every completed-step check uses the declared centered geometry rather than a font-dependent glyph.
- Designer opens without runtime classes, overlays, observers, sidebar transforms, or loading screens.
- Existing forms using other Style Profiles are unchanged.
- Generated and minified colour adapters both cover every colour-bearing K2 variable in every declared K2 context on the target version.
