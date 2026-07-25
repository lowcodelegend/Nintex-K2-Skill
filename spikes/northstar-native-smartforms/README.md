# Northstar native SmartForms verification

This disposable spike proves the case-management homepage architecture against the live K2 5.1019.25336.3 server without replacing the supplier-nonconformance production Forms.

## Live artifacts

- modern Web Component `northstar-command-palette`: `4e49e5b7-075a-449d-b36b-e13c0041978d`;
- Style Profile `Northstar Native Homepage`: `e6a8dfee-e55c-482c-ba10-8b447a22692e`, version 2;
- Form `NTH.Quality Operations`: `1c9553d1-e177-4ca0-b683-2486104ea452`;
- palette View `NTH.Command Palette`: `cd7aa945-d09f-4c97-aaed-588db5f48b58`;
- navigation View `NTH.Application Navigation`: `6155f049-3a3b-475d-847b-3234a767ddb8`.

The full `smartforms-manifest.json` owns eight Views and one Form. `k2forms 0.38.0` verified every definition, Designer hydration, Style Profile identity, `useLegacyTheme=false`, and `preFill=disabled`. The palette View initializes from the user-scoped `SNC_SupplierNonconformance_SNC_CommandSuggestion.List` method and raises a View-owned native `Navigate` action.

## Data security

`SNC.CommandSuggestion(@UserFQN)` returns at most 50 deterministic rows. `k2sql 0.7.0` maps `UserFQN` to K2 `ConnectedUserFQN` in the SmartObject definition and removes the caller-visible identity input. SQL filters owned cases and assigned open tasks before data reaches Runtime.

## Visual acceptance status

Authenticated Runtime capture was attempted on 2026-07-25. The CDP capture host failed before producing the first screenshot with a CLR `Releasing the double mapped memory failed` fatal error after launching Edge. The generated browser profile was removed. K2 route verification therefore remains `reachable-authentication-required`, not authenticated visual proof.

Do not unregister `northstar-case-homepage` yet. It remains the visual oracle until fresh native screenshots pass strict comparison at desktop (1440×1000), laptop (1280×800), tablet (768×1024), and mobile (390×844), including focus, keyboard, empty, long-content, and overflow states.

`palette-view-manifest.json` is the narrow regression fixture that exposed and now proves the duplicate association-Field-ID fix. The full manifest is the canonical disposable native homepage proof.
