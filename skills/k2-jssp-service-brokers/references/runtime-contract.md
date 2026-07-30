# JSSP runtime and deployment contract

## Runtime boundary

K2 runs the bundle in a constrained browser-like JavaScript engine. Supported integrations use asynchronous `XMLHttpRequest` or `fetch`, `postSchema`, `postResult`, console diagnostics, SmartObject APIs, and K2-managed OAuth helpers. Do not rely on Node's `fs`, `net`, `child_process`, `process`, CommonJS loading, native add-ons, browser DOM APIs, temporary storage, or synchronous XHR.

Build tools may use Node and npm. Runtime dependencies must be browser-compatible and bundled into the single output file.

## Schema design

- Flatten nested upstream objects into explicit primitive properties.
- Keep names stable even when the upstream API renames presentation fields.
- Limit methods and outputs to the business contract.
- Treat upstream arrays and nulls deliberately.
- Validate input type/range and `encodeURIComponent` values.
- Allowlist scheme and host; do not accept an arbitrary URL as a method input.

## Deployment

`k2jssp.ps1` uses the installed system SmartObject `com_K2_System_SmartObjects_SmartObject_JavaScriptServiceProvider` and its idempotent `CreateOrUpdateFromFile` method. It uploads the bundle directly, registers or refreshes one Service Instance, and regenerates SmartObjects.

The `.jssp` is stored as readable script content. Never embed tokens, passwords, client secrets, private keys, or connection strings. Use K2-managed authentication facilities or a classic broker when the authentication model is more complex.

Refreshing is mandatory after changing `ondescribe`. Deleting a script is separate from deleting generated SmartObjects and its Service Instance; cleanup therefore resolves and deletes in dependency order.

## Primary references

- [Nintex: JavaScript Service Provider](https://help.nintex.com/en-US/nintexautomation/devref/5.9.1/Content/Extend/JS-Broker/JSServiceBroker.htm)
- [Nintex: JSSP structure](https://help.nintex.com/en-US/nintexautomation/devref/5.9/Content/Extend/JS-Broker/JSSPStructure.htm)
- [Nintex: considerations and limitations](https://help.nintex.com/en-US/nintexautomation/devref/5.9/Content/Extend/JS-Broker/JSSPConsiderations.htm)
- [Nintex: register a JSSP](https://help.nintex.com/en-US/nintexautomation/devref/5.8.1/Content/Extend/JS-Broker/JSSPRegister.htm)
- [Official JSSP template](https://github.com/K2Documentation/K2Documentation.Samples.JavascriptBroker.Template)
