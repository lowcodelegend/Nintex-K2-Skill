# Northstar native SmartForms homepage

This example reproduces the Northstar case-operations homepage while keeping K2 native artifacts authoritative.

## Ownership

- The `Application navigation` List View supplies governed routes.
- The `Command palette` View hosts the registered `northstar-command-palette` modern Web Component and owns its navigation rule.
- Metric, chart, chart-data, and urgent-work Views remain native SmartForms controls over governed SmartObjects.
- The Style Profile adds only application chrome, responsive arrangement, and presentation behavior.

The shell never moves a live K2 View out of its Form ownership tree. It marks the command-palette row and positions it visually in the top bar; K2 bindings, events, lifecycle, and Designer hydration remain intact.

On a guided Form, K2 normally hides the whole first-screen panel after Continue. The shell therefore also marks the palette's owning panel. The stylesheet keeps only that one marked row operational as a zero-layout surface while the rest of the inactive panel remains hidden. The same single control and View-owned navigation rule consequently work on every journey screen.

On a `$k2-smartforms` guided journey, the shell detects the stable generated journey controls, keeps the original `ul.tab-box-tabs` in its native Form tab box, and restyles that exact strip as the Northstar stepper. It does not clone tab anchors or panels. The generated journey title and description become the page introduction, each active panel becomes the central form card, and an injected guidance aside remains presentation-only. Future tabs reject direct pointer activation until native Continue/Save/Focus reaches them, preventing the styled tab surface from bypassing K2 validation. Completed ticks are fixed-size CSS geometry centered independently of font metrics. On mobile, the same native strip becomes a horizontally scrollable compact stepper.

The test-only `Pre-fill` action belongs at the bottom of the first physical journey screen. Back remains a left-aligned neutral action; Continue, Save, Finish, and Submit are right-aligned primary actions. The shell preserves native K2 validation and adds a live summary, invalid count, ARIA state, and first-error focus/scroll. Its K2 5.10 localization fallback is installed only when `locValidationExpresssionsFailed` is absent.

Metric cards follow the same rule: the shell adds classes and presentation metadata to the existing K2 Table cells, and CSS lays those cells out as cards. It never reparents a bound Label/Data Label or copies its value into a replacement dashboard. Accessible chart-data Views also remain native; add any reveal/export interaction as a native SmartForms control and View-owned rule, not as injected Style Profile markup.

Application-navigation labels use the Northstar prototype's neutral grey and turn white only for hover, keyboard focus, and the active route. Keep those shell-scoped selectors more specific than the general K2 link-accent rule; otherwise K2's violet hyperlink colour bleeds into every sidebar route.

## Adaptation

Copy this directory into a solution workspace and edit `northstar-config.js`:

- set brand and signed-in-user presentation;
- map the solution's exact semantic View titles;
- map `NEW_CASE`, insight, and other actions to codes already returned by the governed navigation SmartObject;
- replace the example `pages` entry with the solution's Form name and approved page/insight copy;
- preserve the environment common framework by default because its Views may own required server-load transfers and completion rules. Use `suppressedFrameworkViews` for stable control names or `suppressedFrameworkPanelNames` for stable native panel names only after inspecting the authenticated Runtime DOM; do not use generated GUIDs or remove lifecycle Views merely to change page chrome.
- keep `enableDashboardComposition` enabled in accepted builds; set it to `false` only during a documented browser-isolation pass.
- keep the governed `Application navigation` List View on the first screen of every guided Northstar Form so cold direct links can reconcile the complete authorised shell; the case-management compiler does this automatically.

Keep `Application navigation` and `Command palette` as the exact visible View titles. `commandPaletteViewTitle` is the explicit Style Profile side of the shared case-compiler contract; the mapping's `homepage.commandPalette.viewTitle` must match it, including when an agent-enabled palette clone replaces the ordinary View. The shell positions the live K2 row from the host's measured rectangle without reparenting it, records that state under `window.__k2spNorthstar.commandPalette`, and hides/disables the fallback only after finding the real View. Do not put SQL calls, credentials, authorization filtering, lifecycle decisions, persistence, or workflow actions in the Style Profile.

## Browser loop

Deploy the profile and native Form, then capture the authenticated Runtime at 1440×1000, 1280×800, 768×1024, and 390×844. Use the case-management capture script, compare every image to the supplier-nonconformance gold-standard prototype, correct unexplained structural or visual regions, and repeat until the acceptance gate passes.

The browser driver uses one disposable Edge profile per run and Node's built-in DevTools WebSocket (`node --experimental-websocket`). A screenshot is accepted only when the native Runtime has released its loading lifecycle and the Northstar shell reports both styles and content ready.

For validation proof, pass `--click-name btnJourneyContinue1` to the generic driver, or map the Form/action through `-ValidationClickNames` in `capture-k2-runtime-ux-evidence.ps1`. Assert the exact expected invalid count where it is known.

For post-navigation palette proof, repeat `--click-name` in the real action order, add `--dismiss-dialogs` when Pre-fill or Save displays native feedback, and pass `--palette-probe-text <text>`. Add `--assistant-probe` to record fixed-overlay layout, duplicate counts, modal precedence, close cleanup, and focus return. The driver uses `CDP.Input.dispatchMouseEvent` for native action and palette clicks; it never substitutes `HTMLElement.click()`. The suite wrapper exposes the same flow through `-InteractionClickNames @{ '<Form>' = @('btnPreFill','btnJourneyContinue1') }` and `-CommandPaletteProbeTexts @{ '<Form>' = '<text>' }`. It rejects a visible inert fallback, an unavailable or unfocused real palette, click/Ctrl+K duplication, missing governed results, off-contract completed-tick geometry, overflow, and actionable diagnostics.
