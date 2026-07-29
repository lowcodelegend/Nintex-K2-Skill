---
name: k2-style-profiles
description: Create, host, deploy, inspect, verify, update, and safely remove self-hosted Nintex K2 Five Style Profiles backed by custom CSS and JavaScript. Use for new Style Profiles, complete K2 theme-variable colour schemes, same-origin IIS asset hosting, SmartObject-backed cross-Form sidebars, relocated native K2 Form tabs, anti-flash boot coordination, runtime-only Designer isolation, cache-safe asset revisions, or preparing a Style Profile for k2-smartforms. Do not use for cloud Nintex Forms, custom control registration, direct K2 database edits, or styling an existing form without a Style Profile.
---

# K2 Style Profiles

Create checked-in K2 Style Profiles from declarative manifests, host their CSS/JavaScript in IIS, and verify both the K2 artifact and the bytes served to Runtime.

## Workflow

1. Confirm this is self-hosted K2 Five on a Windows K2 development server. Read [design.md](references/design.md), [manifest.md](references/manifest.md), and [cli.md](references/cli.md). For a colour scheme, read [color-schemes.md](references/color-schemes.md). For a sidebar between Forms, read [smartobject-sidebar.md](references/smartobject-sidebar.md). For a native case-management homepage, use [the Northstar native homepage example](assets/examples/northstar-native-homepage). For one Form whose native K2 tabs become the sidebar, read [native-tabs-sidebar.md](references/native-tabs-sidebar.md). Use the matching complete example.
2. Copy `assets/template` into the solution workspace. Preserve its runtime-only CSS scope and JavaScript Designer guard. Edit `palette.json`, generate the K2 variable adapter from the target server's installed `Variables_Dynamic.css`, and require complete variable plus contextual coverage before visual polish.
3. Choose an existing K2 category, unique system/display names, a same-origin HTTPS asset URL, an isolated physical directory, and an IIS virtual path. Never reuse K2 product directories.
4. Define CSS and JS files in exact load order. Put the generated K2 colour adapter first, solution tokens/reset and component styles next, and behavior JavaScript last. Use a new target file name when browser cache invalidation is required.
5. Run `scripts/k2style.ps1 doctor --manifest <path>`, then `plan`. Resolve collisions, foreign checkouts, invalid hosting mappings, missing sources, mixed content, and Designer-isolation failures before mutation.
6. Run `deploy --manifest <path> --confirm`. Deployment creates or updates the IIS virtual directory, copies declared assets, and uses one explicitly authenticated K2 `FormsManager` connection to load, deploy, and check in the Style Profile. K2 derives the author from that connection; the CLI never supplies or spoofs an author string. Deployment then verifies metadata, category, ordered files, HTTPS responses, MIME types, and source/served hashes.
7. Run `inspect` for GUID/version evidence. Apply the exact Style Profile name or GUID from that output in `$k2-smartforms` only to Forms that use it; set `form.useStyleProfile=false` for plain modern Forms and reconcile stale bindings. Keep every modern Form on `useLegacyTheme=false`.
8. Test authenticated Runtime Forms at desktop and mobile widths. Also open the Form and Style Profile designers and confirm that custom runtime UI, overlays, loaders, and DOM manipulation do not execute there. For shell work, configure and run `scripts/test-runtime.ps1`; do not accept an unmeasured cold load, warm transition, timeout, or Designer boundary.

## Design gates

- Treat CSS/JS as application code. Source-control it, review it, and use Content Security Policy-compatible code without `eval` or remote script injection.
- Apply colour schemes through K2's installed dynamic variable contract before writing selector overrides. Cover every colour-bearing variable in every K2 context reported by `test-k2-color-scheme.ps1`; counts vary by K2 version. Preserve distinct hover, focus, selected, disabled, read-only, error, warning, success, chart, icon, dialog, list, tab, input, and Worklist roles.
- Keep the small semantic palette as the source of truth. Regenerate after a K2 upgrade, validate both readable and minified CSS, load the adapter before polish, and use direct selectors only for proven gaps outside the variable contract.
- Keep every CSS selector runtime-scoped under `html:not(.designer)` or an equally strict reviewed guard. A marker only records a deliberate review; it does not make an unsafe stylesheet safe.
- Put `/* k2style: designer-guard */` beside the JavaScript function that returns before any DOM mutation when the URL or root element indicates Designer mode.
- Prefer same-origin HTTPS URLs. Runtime HTTPS plus HTTP assets is mixed content and must fail planning.
- Keep file order deterministic. CSS cascade and JavaScript initialization depend on it.
- Split shell assets into small critical CSS, a boot coordinator, and asynchronously loaded application CSS. Use `hosting.additionalFiles` for hosted assets that must not become render-blocking K2 references.
- Use narrowly prefixed classes, attributes, custom properties, and events. Do not target brittle generated IDs or unqualified K2 elements when a stable semantic hook can be added.
- Make DOM transforms idempotent and reversible. Mark transformed nodes, tolerate partial postbacks, disconnect observers when no longer needed, and never duplicate navigation, loaders, or event handlers.
- Decorate native KPI/chart/list controls in place. Add narrowly prefixed classes or data attributes to their existing containers; never move bound K2 controls into replacement cards or hide a native control after copying its value into injected markup. Reparenting a live control can deadlock Runtime updates and breaks K2 ownership.
- Move native K2 tab nodes rather than cloning their anchors. Preserve K2 IDs, handlers, rules, panels, Worklist behavior, and programmatic selection; fail open when a competing sidebar already owns the Form.
- For the Northstar case homepage, style and arrange native semantic Views; do not replace them with injected authoritative buttons, charts, tables, or a full-page control. Keep the exact `Application navigation` and `Command palette` View titles, treat `commandPaletteViewTitle` as the explicit shared compiler/profile contract, server-filter the palette projection, and prove authenticated visual parity against the gold-standard prototype at every required viewport. Never reparent the live command-palette View: retain its K2 ownership tree, position its marked row from the palette host's measured geometry, hide the inert fallback only after the real View is found, and verify real CDP pointer input plus Ctrl/Cmd+K each open the same palette exactly once.
- A guided Form may place the command-palette View on its first physical screen, but the palette must remain usable after every native Continue/Save transition. Mark both its stable row and owning native panel; when K2 hides that panel, keep only the palette row alive as a zero-layout, fixed interaction surface. Do not reveal sibling first-screen rows, duplicate the control, or move it out of its View.
- Render completed guided-step marks with centered, size-stable CSS geometry inside the native indicator. Do not use a font check glyph alongside hidden step text; glyph metrics vary by font and can displace the mark.
- Defer expensive work until the DOM exists. Avoid synchronous network calls, broad mutation observers, layout thrashing, and full-document rescans.
- Fail open on both JavaScript and CSS paths. Gate reveal on actual readiness, use two animation frames before reveal, and test flashes only after first contentful paint.
- Build accessible focus, keyboard, reduced-motion, contrast, error, empty, loading, and read-only states. Style Profile polish must not hide native validation or task controls.
- Any CSS contract that restyles native input borders or backgrounds must include a later, equally or more specific invalid-state contract for TextBox/TextArea and memo wrappers, dropdown/select buttons, calendars, checkboxes, and File controls. If the neutral rule uses `!important`, the invalid treatment must too; `k2style doctor` rejects an unprotected important override.
- Guided-journey enhancement may add a runtime-only, non-authoritative validation summary around native K2 validation. It must count the active screen's invalid controls, set `aria-invalid`, update as fields recover, and focus/scroll the first failure. Probe the K2 5.10 misspelled `locValidationExpresssionsFailed` global and install a fallback only when it is absent; never overwrite an installed value or edit K2 product resources.
- Authenticated journey verification must advance through the real named K2 actions, open and type into the real command-palette shadow control after navigation, and measure completed-step indicator geometry. Test desktop and mobile, require palette focus/results, and reject overflow or actionable browser diagnostics.
- Keep `replaceExisting=false` initially. Enable replacement only for the exact intended profile after reviewing `plan`.
- Use unique development names. Do not replace system/internal profiles.

## Creation contract

K2 Five exposes public Style Profile discovery, definition, checkout, deployment, check-in, consumer lookup, and deletion APIs through `FormsManager`. The CLI version-gates `GetStyleProfileDefinition`, `Deploy`, and `CheckInStyleProfile`, then invokes all three on the same connection opened from the manifest's K2 authentication settings. This preserves non-integrated identities such as `K2SQL:K2Admin` through the authoring operation instead of falling back to the utility process's ambient Windows identity. `doctor` verifies the connected authentication mode and security label, reports the effective author context without exposing credentials, and fails safely if the required contract is absent on another K2 version.

The target category must already exist. Create application categories through the owning solution workflow before deploying the Style Profile.

## Safety and cleanup

`cleanup --confirm` deletes only the exact manifest-resolved profile and refuses system/internal or in-use profiles. Add `--assets` only to remove the exact declared hosted files; it retains the physical directory and IIS virtual directory so unrelated files are never recursively deleted.

Do not edit K2 databases, copy assets into K2 installation folders, disable authentication, weaken TLS, overwrite a mismatched IIS mapping, or check in another designer's unreviewed work.

The bundled CLI is the operational capability boundary. During ordinary use, rely on these references and command output rather than inspecting source. Explicit tool-development requests may modify and repackage the repository implementation.
