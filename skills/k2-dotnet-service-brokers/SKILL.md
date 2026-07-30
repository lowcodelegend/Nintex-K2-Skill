---
name: k2-dotnet-service-brokers
description: Build, test, deploy, register, inspect, verify, update, and safely remove classic self-hosted Nintex K2 Five Service Brokers written against the .NET C# ServiceSDK. Use for advanced providers requiring filesystem or OS access, native/vendor SDKs, custom authentication, binary protocols, dynamic discovery, complex execution semantics, or capabilities outside the constrained JSSP engine. Do not use for ordinary SQL, SmartBox, REST, SmartForms, workflows, or lightweight HTTP response shaping that a built-in broker or k2-jssp-service-brokers can handle.
---

# K2 .NET Service Brokers

Treat a classic broker as trusted server code. Keep the contract narrow, validate every configured boundary, avoid secrets in logs, and deploy the exact assembly to every K2 application server in the logical environment.

## Start every project

1. Read project feedback instructions and initialize the standard skill-learning loop through `k2-builder` when absent.
2. Resolve the durable K2 environment and run authenticated commands through `k2env.ps1 invoke`.
3. Read [references/sdk-contract.md](references/sdk-contract.md).
4. Copy [assets/examples/advanced-provider](assets/examples/advanced-provider) to the project. Replace its fixed example GUIDs and names for a real provider.
5. Decide static attributed schema versus dynamic `DescribeSchema`. Prefer static attributes unless discovery genuinely depends on the configured endpoint.

## Required workflow

1. Define configuration, schema, execution, security, timeout, and cleanup boundaries before coding.
2. Derive the broker entry class from `ServiceAssemblyBase`. Set `ServicePackage.IsSuccessful` and add sanitized `ServiceMessages` on failure.
3. Build against the locally installed `SourceCode.SmartObjects.Services.ServiceSDK.dll`; never redistribute K2 SDK binaries in the project or skill.
4. Unit-test provider logic without K2, then build:

   ```powershell
   & '<skill>\scripts\build-broker.ps1' -ExampleRoot '.\broker' -Clean
   ```

5. Run `doctor` and `plan`. Deployment copies one owned DLL to `K2\ServiceBroker`, restarts `K2 Server`, registers/updates one fixed Service Type, creates/refreshes one Service Instance, and generates its SmartObjects:

   ```powershell
   & '<k2-builder>\scripts\k2env.ps1' invoke --name <environment> --command powershell `
     -NoProfile -ExecutionPolicy Bypass -File '<skill>\scripts\k2broker.ps1' `
     deploy -Manifest '.\broker-manifest.json' -Confirm
   ```

6. Verify the exact GUIDs and smoke-test methods. Refresh every Service Instance and regenerate SmartObjects after schema changes.
7. In a multi-server farm, copy the identical signed/release assembly to every application server before registration/restart. Fail the plan if this cannot be guaranteed.
8. Clean up in dependency order. The script refuses generic names and acts only on the fixed Service Type GUID and assembly filename in the manifest.

## Engineering rules

- No direct K2 database changes or product DLL replacement.
- Do not swallow exceptions. Return useful, sanitized ServiceSDK messages.
- Bound filesystem roots, response sizes, row counts, execution time, and network destinations.
- Never accept an arbitrary assembly path, type name, URL, command, or filesystem root from a SmartObject method.
- Use least privilege for the K2 Server service identity.
- Vendor dependencies belong beside the broker only when their license permits and their hashes are recorded.
- Consider strong naming and Authenticode signing for production.
- Do not stop/start K2 from MSBuild events; deployment owns service coordination explicitly.

## Completion evidence

Report build/tests, deployed DLL hash, Service Type/Instance GUIDs, generated SmartObjects, smoke tests, K2 Server recovery, farm coverage, and the exact cleanup command. Record reusable findings in the project learning queue.
