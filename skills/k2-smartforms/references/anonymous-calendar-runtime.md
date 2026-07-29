# Anonymous Calendar culture-request compatibility

Use this compatibility path only when all of these conditions apply:

- K2 build `5.1020.26118.1` or lower;
- Runtime authentication uses OIDC or forms authentication;
- the Form is anonymous; and
- the Form contains a native Calendar control, including generated `Date` or `DateTime` fields.

An old build alone is not proof of the defect. Probe the published Form before changing it:

```powershell
scripts/test-anonymous-calendar-culture.ps1 `
  -RuntimeUrl 'https://k2.example/Runtime/Runtime/Form/APP.Form/'
```

The probe uses requests with cookies disabled. It reports only safe metadata and booleans; it never prints or persists the anonymous token. Apply the workaround only when `defectPresent` is `true`: the exact culture request returns a non-XML response without the token and `application/xml` with K2's existing anonymous token.

## Workaround

Copy [k2-anonymous-calendar-culture-token.v1.js](../assets/compatibility/k2-anonymous-calendar-culture-token.v1.js) into the affected solution's Style Profile workspace. Use `$k2-style-profiles` to host it on the same HTTPS origin and declare it as the first behavior JavaScript file so it executes before K2 initializes the Form controls. Apply that Style Profile only to the affected anonymous Form. If the profile already has scripts, preserve their relative order after this compatibility file.

```json
{
  "type": "js",
  "source": "assets/k2-anonymous-calendar-culture-token.v1.js",
  "target": "k2-anonymous-calendar-culture-token.v1.js"
}
```

The shipped asset is Designer-guarded and idempotent. It returns unless `window.__runtimeIsAnonymous === true` and both existing anonymous-token globals are available. It intercepts only the same-origin request:

```text
AJAXCall.ashx?method=getCulturesListAndCurrentCultureDetailsAndTimezones
```

It adds the existing header with `xhr.setRequestHeader(window.__runtimeAnonTokenName, window.__runtimeAnonToken)`. It does not create, copy, log, or persist a token. Missing values and header failures fail open. Never replace this with anonymous authorization for the entire `AJAXCall.ashx` handler, a global XHR interceptor, a token literal, or a K2 product-file edit.

Every asset change requires a new versioned filename and matching Style Profile manifest entry; changing query strings is not sufficient cache invalidation.

## Cookie-free browser verification

Use a new Edge InPrivate/Guest session or a disposable empty `--user-data-dir`; do not reuse the authenticated Runtime verification profile. Open DevTools before loading the Form, preserve the Network log, and reload:

1. Confirm the Form renders anonymously and contains the native Calendar control.
2. Filter Network by `getCulturesListAndCurrentCultureDetailsAndTimezones`.
3. Confirm that exact request has an `X-K2-Token` request header. Inspect presence only—never record its value or export a HAR.
4. Confirm a 2xx response with `Content-Type: application/xml`.
5. Confirm the Console has no culture/XML parser error and the Calendar opens normally.
6. Confirm the versioned compatibility file loaded before the culture request.

Also prove the script does not run in Designer, on a non-anonymous Form, or for unrelated `AJAXCall.ashx` methods. Record redacted boolean evidence only; screenshots must not show request-header values. After a K2 upgrade, probe again without the asset and remove the workaround when the defect is absent.
