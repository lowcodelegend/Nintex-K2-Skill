# Create the isolated Style Profile

The public K2 FormsManager API on this environment reads, assigns, verifies, checks in and packages Style Profiles, but rejected creation of a new profile. Create this one first-class artifact in Designer:

1. In `K2 Skills > GUX.Gold UX Spike`, create a Style Profile.
2. Set display name and system name to `GUX Northstar`.
3. On **Developer**, link these external files in this order:
   - CSS: `https://spk2.trials.demome.tech/GUXAssets/gux-northstar.css`
   - JavaScript: `https://spk2.trials.demome.tech/GUXAssets/gux-northstar.js`
4. Check in the Style Profile.

Then run:

```powershell
& 'skills/k2-smartforms/scripts/k2forms.ps1' deploy `
  --manifest 'spikes/gold-standard-smartforms/smartforms-manifest.json' `
  --confirm --forms-only
```

This replaces only `GUX.Gold Command Centre` over its already deployed GUX Views. It does not modify an SNC Form, View, workflow, or Style Profile.
