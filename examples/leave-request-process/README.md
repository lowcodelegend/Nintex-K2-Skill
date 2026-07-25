# LPR.Leave Request Process

Declarative K2 Five solution containing:

- SQL-backed leave type, leave status, and leave request SmartObjects;
- a modern tabbed SmartForms application with native My Tasks;
- a single-stage native K2 approval workflow;
- the `LPR Blue and White` Style Profile and same-origin runtime CSS.

The Style Profile loads a generated full K2 variable adapter before its
solution-specific polish. Regenerate and validate it against the target K2
server before deployment:

```powershell
& '.\examples\leave-request-process\build-style-assets.ps1'
```

The adapter is derived from the installed K2 `Variables_Dynamic.css`; the
coverage counts are version-specific and the build fails if any colour-bearing
variable or contextual redeclaration is omitted.

The manager destination is explicitly set to the environment-bound placeholder
`K2:TRIALS\Administrator`. Replace it with the production manager identity or
group in `workflow-manifest.json` before production use.
