# Complete K2 colour schemes

K2's modern `_Dynamic` theme exposes hundreds of CSS custom properties. Those properties drive page and panel surfaces, view chrome, dialogs, toolbars, inputs, buttons, calendars, lists, tabs, selection states, icons, Worklist controls, progress controls, charts, errors, warnings, disabled/read-only states, and nested selected-row contexts. Styling a few native selectors leaves the inherited K2 palette visible elsewhere.

## Build contract

1. Define semantic roles in a small `palette.json`; start from `assets/template/palette.json`.
2. Generate the adapter from the target server's installed `Variables_Dynamic.css`. Do not reuse a catalog from another K2 version:

```powershell
& '<skill>\scripts\new-k2-color-scheme.ps1' `
  -Palette '.\palette.json' `
  -Output '.\k2-color-scheme.css'
```

3. Prove complete variable and context coverage:

```powershell
& '<skill>\scripts\test-k2-color-scheme.ps1' `
  -Css '.\k2-color-scheme.css'
```

The reported counts are version-specific. On the K2 5.10 validation server, the contract currently contains 410 colour-bearing variables; contextual declaration counts are derived at build time. Never encode those numbers as universal constants; require actual equals expected.

4. Minify and validate the production file again:

```powershell
& npx.cmd --yes esbuild@0.25.6 '.\k2-color-scheme.css' `
  --minify --target=chrome120 `
  '--outfile=.\k2-color-scheme.min.css'

& '<skill>\scripts\test-k2-color-scheme.ps1' `
  -Css '.\k2-color-scheme.min.css'
```

5. Load the minified adapter first in `styleProfile.files`, followed by solution-specific layout/polish CSS and then JavaScript. Use a new versioned target filename when changing it.

The generator preserves K2's context selectors and adds the Runtime/Designer guard. This matters because nested Forms, dialogs, toolbars, and selected list rows redeclare variables at greater specificity; a single `.theme-entry` palette block cannot override all of them.

## Semantic roles

Keep accent, strong/soft/subtle accent, on-accent, page, surface, alternate surface, text, muted text, border, focus, danger, warning, success, disabled, shadow, and chart-series roles distinct. Do not reduce every state to one brand colour. The adapter maps K2's platform names to those roles while preserving recognizable hover, focus, selected, error, warning, disabled, and read-only behavior.

Use direct selectors only after the adapter passes and the browser proves a remaining colour comes from a surface outside the variable contract—for example, solution content, a bitmap/logo, or a legacy/custom control. Record such selectors as explicit compatibility polish rather than treating them as the theme foundation.

## Visual gate

Capture authenticated Runtime at desktop and mobile widths and exercise:

- view headers, collapse/expand icons, toolbars, list headers, zebra rows, paging, hover, focus, and selection;
- text, dropdown, lookup, picker, date, checkbox, radio, file, validation, disabled, and read-only controls;
- primary, quiet, destructive, disabled, hover, and keyboard-focus buttons;
- tabs, dialogs, menus, tooltips, calendars, Worklist, progress, charts, badges, errors, and warnings.

Compare computed values or screenshots against the semantic palette. Also open Form and Style Profile Designers and confirm the adapter remains inert there.

Automate the authenticated Runtime inventory when the Form is available:

```powershell
& '<skill>\scripts\audit-runtime-colors.ps1' `
  -Url 'https://k2.example.test/Runtime/Runtime/Form/My.Form/' `
  -OutputDirectory '.\.artifacts\theme-audit' `
  -TrustedAuthHost 'k2.example.test' `
  -ExpectedStylesheetPattern 'k2-color-scheme\.v1\.min\.css'
```

Retain its screenshot and JSON while iterating. The audit requires the expected
stylesheet, all discovered K2 colour variables, and an overflow-free page; it
also records computed foreground, background, border, fill, and contrast values
for representative native surfaces.
