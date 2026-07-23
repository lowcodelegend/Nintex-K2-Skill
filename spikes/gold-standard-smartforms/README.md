# GUX native-shell SmartForms spike

An isolated two-page proof of concept for translating the accepted Northstar web UI into native K2 SmartForms without an iframe or a master-page dependency.

- K2 root: `K2 Skills\GUX.Gold UX Spike`
- Style Profile: `GUX Northstar [GUX Northstar]`
- Hosted assets: `https://spk2.trials.demome.tech/GUXAssets/`
- Shell asset revision: `2026.07.23.5`
- Navigation database: `GUX.GoldUXSpike`
- Navigation Service Instance: `GUX.GoldUXSpikeSql`
- Existing SNC dashboard SmartObjects remain read-only dependencies. No SNC Form, View, Style Profile, or workflow is replaced.

## Responsibility split

1. SQL and the generated `GUX_GoldUXSpikeSql_GUX_ApplicationNavigation` SmartObject own the reusable navigation contract.
2. Native SmartForms owns SmartObject execution, metric bindings, charts, list data, tabs, Worklist loading, and K2 security.
3. The Style Profile's existing CSS URL serves a runtime-gated critical boot layer so the branded surface paints before the full application theme.
4. Hosted JavaScript activates only on K2 Runtime Form routes, loads the versioned application CSS behind that surface, renders cached shell chrome, reconciles it with the native navigation View, derives active state from the top-level URL, adapts each page, and leaves Designer and non-GUX routes untouched.

The SmartForms manifest deliberately disables the environment's selected PSF header/footer.

## Live entry points

- Command Centre: `https://spk2.trials.demome.tech/Runtime/Runtime/Form/GUX.Gold%20Command%20Centre/`
  - Form GUID: `fdc6da78-b94a-47b9-aa16-fafa5f148f2e`
- My Work: `https://spk2.trials.demome.tech/Runtime/Runtime/Form/GUX.My%20Work/`
  - Form GUID: `9250beb9-1610-4a1e-9648-1cda5da237ff`
- Style Profile GUID: `88cc1563-c7db-4a81-883f-a53ed47d2f68`

## Repeatable deployment

```powershell
& 'skills/k2-sql-smartobjects/scripts/k2sql.ps1' deploy `
  --manifest 'spikes/gold-standard-smartforms/sql-smartobjects-manifest.json' `
  --confirm

& 'skills/k2-smartforms/scripts/k2forms.ps1' deploy `
  --manifest 'spikes/gold-standard-smartforms/smartforms-manifest.json' `
  --confirm

& 'spikes/gold-standard-smartforms/build-assets.ps1'

Copy-Item 'spikes/gold-standard-smartforms/build/assets/gux-northstar.css' `
  'C:\inetpub\gux-assets\gux-northstar.css' -Force
Copy-Item 'spikes/gold-standard-smartforms/build/assets/gux-northstar-app.css' `
  'C:\inetpub\gux-assets\gux-northstar-app.css' -Force
Copy-Item 'spikes/gold-standard-smartforms/build/assets/gux-northstar.js' `
  'C:\inetpub\gux-assets\gux-northstar.js' -Force

& "$env:windir\system32\inetsrv\appcmd.exe" set config 'K2/GUXAssets' `
  /section:system.webServer/urlCompression /doStaticCompression:true /commit:apphost
```

The Style Profile itself must already reference the two hosted asset URLs. See `CREATE-STYLE-PROFILE.md` for the one-time setup.

## Validation

- Final desktop/laptop/tablet/mobile evidence for both Forms: `runtime-native-shell-final/`
- Designer isolation plus native route, cache, dirty-state, stylesheet gate, fallback, and timing report: `runtime-designer-isolation/native-shell-validation.json`
- Cold-load and inter-Form transition recording: `runtime-video-critical/gux-page-load-and-transition.mp4`
- Frame-by-frame load and transition strips: `runtime-video-critical/load-strip.png` and `runtime-video-critical/transition-strip.png`
- Repeat the interaction suite:

```powershell
& 'spikes/gold-standard-smartforms/test-native-shell-cdp.ps1'
```

The latest run proves that optimized revision `2026.07.23.5` is loaded, the Style Profile assets are visually and behaviourally inert under `/designer/`, both Runtime application stylesheets are ready before content reveal, the SmartObject rows are rendered as sidebar links, and the native `GUX.Application Navigation` View has computed `display:none` on both Forms. The deployed assets are minified and gzip-compressed, and the CSS uses the local Segoe UI variable/system stack without an external font request. The Style Profile definition and both Form definitions remain unchanged.

See `deployment-ledger.json` for the itemised live inventory, verification scope, and accepted spike errata.
