# Classic ServiceSDK broker contract

## Provider shape

The broker entry point derives from `SourceCode.SmartObjects.Services.ServiceSDK.ServiceAssemblyBase`.

- `GetConfigSection()` declares Service Instance settings.
- `DescribeSchema()` returns Service Objects, properties, and methods. Static providers can construct a `ServiceObject` from attributed C# classes; dynamic providers build SDK objects after interrogating the endpoint.
- Attributed implementation methods are invoked by K2 and return one record or an array/collection.
- `Extend()` is not implemented unless the underlying provider explicitly supports schema extension.

Keep discovery side-effect-free. Keep execution deterministic, bounded, and cancellation/timeout-aware where the underlying SDK permits.

## Deployment lifecycle

1. Compile for .NET Framework 4.8 against the installed K2 ServiceSDK reference.
2. Copy the broker and licensed dependencies to `K2\ServiceBroker` on every K2 application server.
3. Restart K2 Server in a controlled development/maintenance window.
4. Register one stable Service Type GUID and class.
5. Register or refresh Service Instances.
6. Generate/update SmartObjects and execute smoke tests.

Schema changes are not visible until the Service Instance is refreshed and its SmartObjects regenerated.

## Selection

Use this extension point when the integration requires server filesystem/OS access, vendor/native assemblies, a bespoke security protocol, binary data, dynamic discovery, or execution behavior unavailable to the built-in providers and JSSP. Use JSSP for straightforward asynchronous REST calls and payload flattening.

## Primary references

- [Nintex: custom Service Brokers](https://help.nintex.com/en-US/k2five/DevRef/5.3/Content/Extend/SmO/Custom-Service-Brokers.htm)
- [Nintex: static/described Service Brokers](https://help.nintex.com/en-US/k2blackpearl/devref/4.7/Content/StaticDescribedBrokers.htm)
- [Nintex: register and deploy](https://help.nintex.com/en-US/K2Five/DevRef/5.3/Content/Extend/SmO/RegisterDeploy.htm)
- [Official static sample](https://github.com/K2Documentation/K2Documentation.Samples.Extensions.StaticServiceBroker)
- [Official dynamic sample](https://github.com/K2Documentation/K2Documentation.Samples.Extensions.DynamicServiceBroker)
