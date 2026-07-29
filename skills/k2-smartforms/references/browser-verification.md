# Attended Runtime browser verification

Use this path when the K2 Runtime requires interactive OIDC, forms authentication, MFA, or Conditional Access. Keep management verification and Runtime verification separate: `k2forms verify` proves deployed definitions and route reachability; attended browser evidence proves rendering and user behavior.

## Start and authenticate

Run the browser helper in the signed-in user's Windows session, never from a Windows service:

```powershell
scripts/k2forms-runtime-browser.ps1 Start `
  -RuntimeUrl 'https://k2.example/Runtime/Form/APP.Case/' `
  -AllowedAuthHost 'login.example.com'
```

The helper starts visible Edge with a dedicated profile and a DevTools endpoint bound to `127.0.0.1`. The user completes OIDC and MFA directly in Edge. Do not ask for, record, or automate credentials.

The MCP or service wrapper may launch or monitor a user-session helper, but it must not attempt interactive login from Windows service Session 0. Never expose the DevTools port beyond loopback.

After login:

```powershell
scripts/k2forms-runtime-browser.ps1 Wait
scripts/k2forms-runtime-browser.ps1 Status
```

`Wait` attaches only after a page returns to the recorded Runtime origin. The helper never evaluates or captures an identity-provider page. `Status` removes query strings and fragments and flags external hosts that were not declared with `-AllowedAuthHost`.

## Capture evidence

Exercise the normal Form manually. Capture each material checkpoint with assertions that do not disclose page content:

```powershell
scripts/k2forms-runtime-browser.ps1 Capture `
  -Checkpoint 'create-reload' `
  -ExpectedSelector 'body' `
  -ExpectedText 'Saved' `
  -ExpectedUserText 'K2Admin' `
  -ConfirmManualAction
```

The helper writes a PNG and adjacent JSON under the current user's local application data unless `-Output` selects another `.png` path. Treat screenshots as potentially sensitive. Never commit browser profiles, screenshots, cookies, tokens, or session material.

`operatorAttested=true` means the user confirmed that the named action happened before capture. DOM checks remain boolean evidence; the JSON never contains body text, cookies, headers, query strings, or fragments. If the application does not visibly render the principal, omit `-ExpectedUserText` and record the identity through an approved application-specific user indicator.

Capture at least:

- authenticated initial render and the visible expected identity when available;
- Create followed by reload/read of the persisted record;
- list selection and detail loading;
- Update followed by reload;
- Delete plus confirmation and absence;
- lookup population, validation messages, and responsive layouts;
- workflow start, Worklist population, task open/action, and final state where applicable.

Run `Stop` when finished. It closes only Edge processes using the recorded dedicated profile and retains that profile for approved session reuse. OIDC expiry is expected: start the browser again and let the user reauthenticate. Do not extend a session by copying cookies or tokens.

## Evidence interpretation

Do not promote a route redirect, screenshot, or technical DOM assertion into a behavioral pass by itself. A mutating or workflow checkpoint requires the matching action, a post-action assertion, and `operatorAttested=true`. Report missing checkpoints as skipped errata.

For unattended CI, run management verification only unless an approved interactive Windows runner and dedicated test identity are available. OAuth client credentials, device-code tokens, injected headers, and authentication bypasses do not substitute for a real SmartForms Runtime user session.
