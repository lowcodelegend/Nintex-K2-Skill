---
name: k2-jssp-service-brokers
description: Build, test, deploy, inspect, verify, update, and safely remove self-hosted Nintex K2 Five JavaScript Service Provider (JSSP) brokers. Use for lightweight REST integrations where nested JSON must be flattened or reshaped into stable SmartObject schemas and the implementation can stay inside K2's constrained asynchronous JavaScript engine. Do not use when the provider needs full Node.js, filesystem access, native assemblies, arbitrary npm modules at runtime, synchronous I/O, or advanced provider SDK behavior; use k2-dotnet-service-brokers for those cases.
---

# K2 JSSP Service Brokers

Use a JSSP when the work is principally asynchronous HTTP plus explicit payload shaping. Treat build-time Node tooling and K2's runtime engine as separate environments: bundle development dependencies into one `.jssp` file and never assume Node globals, filesystem APIs, or runtime module loading.

## Start every project

1. Read the project `AGENTS.md` and `docs/skill-learnings.md` when present.
2. Initialize the shared feedback mechanism through `k2-builder/scripts/initialize-skill-feedback.ps1` when absent, naming `k2-jssp-service-brokers` as an owner.
3. Detect the durable K2 environment with `k2env.ps1 show --summary --output json`. Run authenticated operations through `k2env.ps1 invoke`; never introduce a solution-specific password.
4. Read [references/runtime-contract.md](references/runtime-contract.md) before authoring.
5. Copy [assets/examples/rest-shaping](assets/examples/rest-shaping) to the project and change the unique script, instance, and SmartObject names.

## Required workflow

1. Model a small, stable, flat schema. Do not expose an upstream response wholesale.
2. Keep `metadata`, `ondescribe`, and `onexecute` explicit. Reject unsupported objects/methods and validate every input before constructing a URL.
3. Allowlist base URLs and encode all path/query input. Put no secrets in the `.jssp`; the registered script is retrievable as plain text.
4. Unit-test schema and transformation logic with mocked asynchronous XHR.
5. Build one deployable `.jssp` file:

   ```powershell
   & '<skill>\scripts\build-broker.ps1' -ExampleRoot '.\broker'
   ```

6. Run `doctor`, then `plan`. Deploy only after the plan identifies the intended unique names:

   ```powershell
   & '<k2-builder>\scripts\k2env.ps1' invoke --name <environment> --command powershell `
     -NoProfile -ExecutionPolicy Bypass -File '<skill>\scripts\k2jssp.ps1' `
     doctor -Manifest '.\broker-manifest.json'

   & '<k2-builder>\scripts\k2env.ps1' invoke --name <environment> --command powershell `
     -NoProfile -ExecutionPolicy Bypass -File '<skill>\scripts\k2jssp.ps1' `
     deploy -Manifest '.\broker-manifest.json' -Confirm
   ```

7. Verify that the exact Service Type, Service Instance, and generated SmartObjects resolve, then execute their parameterless List smoke tests.
8. After schema changes, update the script, refresh the Service Instance, and regenerate SmartObjects. Never create a second similarly named type to avoid refresh.
9. Clean up only assets whose exact names and owning Service Instance match the manifest:

   ```powershell
   # Destructive: deletes generated SmartObjects, the instance, script, and service type.
   & '<skill>\scripts\k2jssp.ps1' cleanup -Manifest '.\broker-manifest.json' -Confirm
   ```

## Boundary decisions

- Choose JSSP for HTTP APIs, JSON flattening, small calculated fields, and response normalization.
- Choose the built-in REST broker when its generated schema is already stable and usable.
- Choose `k2-dotnet-service-brokers` for filesystem/native SDK access, custom authentication handlers, dynamic discovery, binary protocols, long-running work, or dependencies that cannot be bundled into browser-compatible JavaScript.
- Never edit K2 databases, product configuration, or JavaScript host binaries.

## Completion evidence

Report the script system name, Service Type GUID, Service Instance GUID, generated SmartObject names/GUIDs, test results, and cleanup command. Record reusable findings in `docs/skill-learnings.md`.
