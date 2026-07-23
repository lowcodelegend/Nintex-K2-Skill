# Style Profile manifest

The JSON root contains `schemaVersion`, `name`, `k2`, `styleProfile`, `hosting`, and `verification`.

```json
{
  "schemaVersion": 1,
  "name": "Operations UX",
  "k2": {
    "host": "localhost",
    "port": 5555,
    "integrated": true,
    "securityLabel": "K2"
  },
  "styleProfile": {
    "systemName": "OPS Style Profile",
    "displayName": "OPS Style Profile",
    "description": "Operations runtime UX.",
    "categoryPath": "Operations\\UX",
    "previewFormId": null,
    "replaceExisting": false,
    "files": [
      {
        "type": "css",
        "source": "assets/ops.v1.css",
        "target": "ops.v1.css"
      },
      {
        "type": "js",
        "source": "assets/ops.v1.js",
        "target": "ops.v1.js"
      }
    ]
  },
  "hosting": {
    "enabled": true,
    "configureIis": true,
    "siteName": "K2",
    "applicationPath": "K2/",
    "virtualPath": "/OPSAssets",
    "physicalPath": "C:\\inetpub\\ops-assets",
    "baseUrl": "https://k2.example.com/OPSAssets",
    "additionalFiles": [
      {
        "type": "css",
        "source": "assets/ops-application.v1.css",
        "target": "ops-application.v1.css"
      }
    ]
  },
  "verification": {
    "requireHttps": true,
    "requireDesignerIsolation": true,
    "verifyHttp": true,
    "httpTimeoutSeconds": 20
  }
}
```

## `k2`

- `host`, `port`: K2 host-server endpoint.
- `integrated`: use the current Windows identity when true.
- `securityLabel`: normally `K2`.
- For non-integrated authentication, set `domain`, `userName`, and `passwordEnvironmentVariable`. Store no password in JSON.

## `styleProfile`

- `systemName`, `displayName`: stable, version-free identifiers. Either matching an existing artifact resolves it; if they resolve different artifacts, deployment stops.
- `categoryPath`: existing K2 category path without `Public Folder\`.
- `previewFormId`: optional existing Form GUID.
- `replaceExisting`: false by default. True updates the exact resolved profile in place.
- `files`: ordered CSS/JS contract. `type` is `css` or `js`.
- `source`: path relative to the manifest. Required for managed hosting and byte verification.
- `target`: single destination filename. Paths and traversal are rejected.
- `url`: optional explicit absolute asset URL. When omitted, the CLI combines `hosting.baseUrl` and `target`.

K2 stores file URLs percent-encoded. Manifests always use normal decoded URLs.

## `hosting`

- `enabled=false`: do not copy files or configure IIS; every file must provide `url`.
- `configureIis=true`: create the exact virtual directory if absent. If it exists with another physical path, stop without changing it.
- `applicationPath`: IIS application name, normally `K2/`.
- `virtualPath`: application-relative path beginning with `/`.
- `physicalPath`: dedicated absolute directory outside K2 installation folders.
- `baseUrl`: public URL corresponding to the virtual path. Prefer the K2 Runtime origin and HTTPS.
- `additionalFiles`: optional CSS/JS hosted, guarded, hashed, verified, and cleaned up with the manifest, but deliberately omitted from the Style Profile’s ordered K2 references. Use it for application CSS loaded asynchronously by a critical boot script.

The CLI leaves the IIS mapping and directory in place during cleanup. This prevents broad deletion and lets separately managed assets coexist safely.

## `verification`

- `requireHttps`: reject non-HTTPS managed base URLs.
- `requireDesignerIsolation`: statically gate CSS/JS source before deployment.
- `verifyHttp`: GET every declared URL, validate MIME type, and compare served bytes with `source`.
- `httpTimeoutSeconds`: 1–300.

Use versioned or content-hashed `target` names when caches must be invalidated. Deploy the new files and ordered URL contract together.
