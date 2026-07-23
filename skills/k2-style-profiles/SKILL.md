---
name: k2-style-profiles
description: Create, host, deploy, inspect, verify, update, and safely remove self-hosted Nintex K2 Five Style Profiles backed by custom CSS and JavaScript. Use for new Style Profiles, same-origin IIS asset hosting, SmartObject-backed cross-Form sidebars, relocated native K2 Form tabs, anti-flash boot coordination, runtime-only Designer isolation, cache-safe asset revisions, or preparing a Style Profile for k2-smartforms. Do not use for cloud Nintex Forms, custom control registration, direct K2 database edits, or styling an existing form without a Style Profile.
---

# K2 Style Profiles

Create checked-in K2 Style Profiles from declarative manifests, host their CSS/JavaScript in IIS, and verify both the K2 artifact and the bytes served to Runtime.

## Workflow

1. Confirm this is self-hosted K2 Five on a Windows K2 development server. Read [design.md](references/design.md), [manifest.md](references/manifest.md), and [cli.md](references/cli.md). For a sidebar between Forms, read [smartobject-sidebar.md](references/smartobject-sidebar.md). For one Form whose native K2 tabs become the sidebar, read [native-tabs-sidebar.md](references/native-tabs-sidebar.md). Use the matching complete example.
2. Copy `assets/template` into the solution workspace. Preserve its runtime-only CSS scope and JavaScript Designer guard.
3. Choose an existing K2 category, unique system/display names, a same-origin HTTPS asset URL, an isolated physical directory, and an IIS virtual path. Never reuse K2 product directories.
4. Define CSS and JS files in exact load order. Put base tokens/reset first, component styles next, and behavior JavaScript last. Use a new target file name when browser cache invalidation is required.
5. Run `scripts/k2style.ps1 doctor --manifest <path>`, then `plan`. Resolve collisions, foreign checkouts, invalid hosting mappings, missing sources, mixed content, and Designer-isolation failures before mutation.
6. Run `deploy --manifest <path> --confirm`. Deployment creates or updates the IIS virtual directory, copies declared assets, creates or updates the Style Profile through the installed K2 Designer save implementation, checks it in, and verifies metadata, category, ordered files, HTTPS responses, MIME types, and source/served hashes.
7. Run `inspect` for GUID/version evidence. Apply the exact Style Profile name or GUID from that output in `$k2-smartforms`; keep modern Forms on `useLegacyTheme=false`.
8. Test authenticated Runtime Forms at desktop and mobile widths. Also open the Form and Style Profile designers and confirm that custom runtime UI, overlays, loaders, and DOM manipulation do not execute there. For shell work, configure and run `scripts/test-runtime.ps1`; do not accept an unmeasured cold load, warm transition, timeout, or Designer boundary.

## Design gates

- Treat CSS/JS as application code. Source-control it, review it, and use Content Security Policy-compatible code without `eval` or remote script injection.
- Keep every CSS selector runtime-scoped under `html:not(.designer)` or an equally strict reviewed guard. A marker only records a deliberate review; it does not make an unsafe stylesheet safe.
- Put `/* k2style: designer-guard */` beside the JavaScript function that returns before any DOM mutation when the URL or root element indicates Designer mode.
- Prefer same-origin HTTPS URLs. Runtime HTTPS plus HTTP assets is mixed content and must fail planning.
- Keep file order deterministic. CSS cascade and JavaScript initialization depend on it.
- Split shell assets into small critical CSS, a boot coordinator, and asynchronously loaded application CSS. Use `hosting.additionalFiles` for hosted assets that must not become render-blocking K2 references.
- Use narrowly prefixed classes, attributes, custom properties, and events. Do not target brittle generated IDs or unqualified K2 elements when a stable semantic hook can be added.
- Make DOM transforms idempotent and reversible. Mark transformed nodes, tolerate partial postbacks, disconnect observers when no longer needed, and never duplicate navigation, loaders, or event handlers.
- Move native K2 tab nodes rather than cloning their anchors. Preserve K2 IDs, handlers, rules, panels, Worklist behavior, and programmatic selection; fail open when a competing sidebar already owns the Form.
- Defer expensive work until the DOM exists. Avoid synchronous network calls, broad mutation observers, layout thrashing, and full-document rescans.
- Fail open on both JavaScript and CSS paths. Gate reveal on actual readiness, use two animation frames before reveal, and test flashes only after first contentful paint.
- Build accessible focus, keyboard, reduced-motion, contrast, error, empty, loading, and read-only states. Style Profile polish must not hide native validation or task controls.
- Keep `replaceExisting=false` initially. Enable replacement only for the exact intended profile after reviewing `plan`.
- Use unique development names. Do not replace system/internal profiles.

## Creation contract

K2 Five exposes public Style Profile discovery, definition, checkout, check-in, consumer lookup, and deletion APIs, but this installed version does not expose a public create/save method. The CLI version-gates and invokes K2 Designer's installed `SaveStyleProfile` implementation in-process—the same server implementation used by Designer—without browser automation or database writes. `doctor` fails safely if that contract is absent on another K2 version.

The target category must already exist. Create application categories through the owning solution workflow before deploying the Style Profile.

## Safety and cleanup

`cleanup --confirm` deletes only the exact manifest-resolved profile and refuses system/internal or in-use profiles. Add `--assets` only to remove the exact declared hosted files; it retains the physical directory and IIS virtual directory so unrelated files are never recursively deleted.

Do not edit K2 databases, copy assets into K2 installation folders, disable authentication, weaken TLS, overwrite a mismatched IIS mapping, or check in another designer's unreviewed work.

The bundled CLI is the operational capability boundary. During ordinary use, rely on these references and command output rather than inspecting source. Explicit tool-development requests may modify and repackage the repository implementation.
