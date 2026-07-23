# Native K2 tabs as sidebar navigation

Use this pattern when one K2 Form already owns the application sections as native Form tabs. The Style Profile moves K2’s real `ul.tab-box-tabs` node into an injected responsive sidebar. It does not clone links, replace tab panels, or reimplement K2 rules.

## Contents

- [Choose the pattern](#choose-the-pattern)
- [Artifact set](#artifact-set)
- [Native ownership contract](#native-ownership-contract)
- [DOM contract](#dom-contract)
- [Loading and failure behavior](#loading-and-failure-behavior)
- [Accessibility and responsive behavior](#accessibility-and-responsive-behavior)
- [Implementation](#implementation)
- [Validation](#validation)
- [Known limits](#known-limits)

## Choose the pattern

Use this pattern for sections inside one Form:

- K2’s native tab rules remain authoritative.
- Selecting a List row can activate another tab through `listClickTabNavigation`.
- Native Worklist tabs remain available.
- Switching sections does not perform a full Form navigation.
- Tab availability/order is defined in the SmartForms artifact.

Use [smartobject-sidebar.md](smartobject-sidebar.md) for navigation between separate Forms:

- links are data-driven through a SmartObject;
- authorization or configuration can vary the available routes;
- each selection loads another Runtime Form;
- URL-derived active state and cross-page cache reconciliation are required.

Do not apply two independent sidebar transforms to the same Form. A solution may use cross-Form navigation and native tabs together, but present one as primary navigation and the other as secondary in-content navigation rather than competing fixed sidebars.

## Artifact set

Copy `assets/examples/native-tabs-sidebar` into the solution workspace. It contains:

- `smartforms-fragment.json`: a native tabbed Form with List, Details, and Worklist tabs;
- `tabs-sidebar-critical.css`: render-blocking anti-flash and CSS-only fail-open behavior;
- `tabs-sidebar-config.js`: solution-specific branding, asset URL, icon, and cache settings;
- `tabs-sidebar.js`: runtime guard, relocation, selection synchronization, restoration, and accessibility behavior;
- `tabs-sidebar-application.css`: full desktop, collapsed, mobile, focus, and reduced-motion styling;
- `style-profile-manifest.json`: ordered critical/config/boot references and asynchronous application CSS;
- `fixture.html`: representative K2 Form-tab markup;
- `test-fixture.ps1`: disposable Edge validation;
- `build-assets.ps1`: deterministic minification.

Rename the `APP` prefix, K2 category, Style Profile, physical directory, virtual path, URLs, SmartObjects, Views, Forms, tab names, branding, and icon map. Preserve the ownership, runtime guard, restoration, and fail-open contracts.

## Native ownership contract

Build tabs through `$k2-smartforms`; never hand-author a replacement tab system. The Form manifest remains the source of:

- tab names and order;
- Views contained by each tab;
- Worklist configuration;
- list-click tab navigation;
- rule-driven active-tab changes;
- task and workflow integrations.

The Style Profile owns presentation only. It moves the native strip, adds semantic attributes, listens for native selection changes, and reserves responsive layout space. It must not call private K2 tab functions or hide/replace native tab panels.

Moving the existing node preserves K2’s element IDs, jQuery handlers, tab-to-panel mapping, rule actions, Worklist tab, and programmatic selection behavior. Cloning the anchors would lose those relationships and is prohibited.

## DOM contract

The tested modern K2 Runtime shape is:

```html
<div class="runtime-form">
  <ul class="tab-box-tabs" id="{form-guid}_tabPanel">
    <li><a class="tab selected" id="{tab-id}">...</a></li>
  </ul>
  <div class="tab-box form-tabs" id="{form-guid}_tabbox">
    <div class="formpanel" id="{tab-id}_form">...</div>
  </div>
</div>
```

The script accepts a `ul.tab-box-tabs` only when it has at least two anchors and an associated `.tab-box.form-tabs`. This avoids relocating nested View-level tab controls.

At Runtime it injects `nav#k2sp-tab-sidebar` and moves that exact `ul` into `#k2sp-tab-list-host`. It stores the original parent and sibling so failure can restore the native strip. A narrow observer watches the native strip’s class changes so K2 rule-driven activation updates `aria-selected`, roving `tabindex`, and sidebar highlighting.

Revalidate selectors after every K2 upgrade. If the form-level tab markup changes, fail open and update the example rather than broadening selectors to arbitrary tab-like nodes.

## Loading and failure behavior

Use this ordered loading model:

1. Critical CSS covers the Runtime and suppresses only a raw form-level horizontal tab strip.
2. Configuration JavaScript establishes solution-specific values without DOM mutation.
3. Boot JavaScript requests application CSS from `hosting.additionalFiles`.
4. After application CSS and native form tabs both exist, the script injects the shell and moves the strip.
5. Two animation frames complete before the boot cover is removed.

Critical CSS restores both the cover and native tab visibility after 2.5 seconds if JavaScript is blocked. JavaScript also restores the strip and removes its injected shell when application CSS fails, tabs never appear, or another fixed sidebar already owns the Form.

Keep `allowExistingSidebar=false` in deployed configurations. It prevents double transformation when a profile such as PSF Nintex already injected a sidebar.

## Accessibility and responsive behavior

The script adds, without replacing K2 behavior:

- `role="tablist"` and responsive `aria-orientation`;
- `role="tab"`, one `aria-selected="true"`, and roving `tabindex`;
- `role="tabpanel"` and `aria-labelledby` where K2’s ID mapping is available;
- Arrow, Home, End, Enter, and Space keyboard handling;
- visible focus and active states;
- an accessible collapse/expand button;
- reduced-motion support.

Desktop uses a fixed sidebar and reserves body padding. Collapsed desktop keeps icons and accessible labels while reducing the reserved width. At 800 px and below, the same native tab strip becomes a horizontally scrollable bottom navigation. No second tab list is created.

## Implementation

1. Copy the example directory and replace placeholders.
2. Merge `smartforms-fragment.json` into the solution manifest and use real existing SmartObjects.
3. Keep `useLegacyTheme=false`; declare two or more native Form tabs.
4. Edit `tabs-sidebar-config.js` with the hosted application CSS URL, branding, storage key, and an icon mapping for every tab label.
5. Run `build-assets.ps1`.
6. Run `test-fixture.ps1` and resolve every failed behavior before deployment.
7. Deploy the Style Profile with `k2style`.
8. Select the exact Style Profile on the tabbed Form through `$k2-smartforms`.
9. Browser-test every tab, native list-click navigation, Worklist, task actions, rule-driven tab activation, desktop collapse, mobile overflow, keyboard navigation, and Designer.

Keep the configuration file before the boot coordinator in `styleProfile.files`. Keep application CSS in `hosting.additionalFiles`; loading it asynchronously avoids making the complete visual layer render-blocking.

## Validation

Run:

```powershell
& '.\build-assets.ps1'
& '.\test-fixture.ps1'
```

The fixture uses the native IDs/classes and tab/panel relationship observed in K2 Runtime. It verifies:

- the original strip is moved exactly once;
- there is one selected native tab and no cloned navigation;
- K2-style click and programmatic class changes synchronize the sidebar;
- tab/panel roles and keyboard movement;
- desktop space reservation and collapse state;
- mobile bottom-navigation orientation and overflow;
- Designer isolation;
- CSS-only recovery when JavaScript is blocked.

Fixture success is necessary but not sufficient. On a deployed Form also test real K2 rules and partial updates. The example was forward-tested by injecting it into a disposable browser session over a deployed nine-tab K2 Form: the original strip relocated, all nine tabs remained present, native selection changed from `Cases` to `Overview`, one selected tab remained, and no horizontal overflow occurred. No K2 artifact was changed by that test.

## Known limits

- This is within-Form navigation; it does not provide routes to other Forms.
- Tab order and visibility remain Form-definition concerns, not a SmartObject data contract.
- Do not combine it with another Style Profile that already moves native tabs.
- More tabs than fit on mobile scroll horizontally; prioritize concise names and a manageable section count.
- The narrow selection observer intentionally remains attached to the relocated strip because K2 rules can change the active tab at any time.
- If K2 replaces the strip during a partial update, the runtime observer relocates the replacement and discards the stale copy. Re-test unusually dynamic custom controls.
