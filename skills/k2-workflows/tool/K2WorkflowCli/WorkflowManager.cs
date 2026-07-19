using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace K2WorkflowCli
{
    internal sealed class WorkflowManager : IDisposable
    {
        private const int CategorySystemId = 1;
        private readonly WorkflowManifest _manifest;
        private readonly object _client;
        private readonly Type _clientType;
        private readonly string _userName;

        public WorkflowManager(WorkflowManifest manifest)
        {
            _manifest = manifest;
            var identity = WindowsIdentity.GetCurrent();
            _userName = identity.Name;
            System.Threading.Thread.CurrentPrincipal = new WindowsPrincipal(identity);
            _client = CreateDesignerClient();
            _clientType = _client.GetType();
        }

        public static void Doctor()
        {
            var webBin = RuntimeAssemblyResolver.WorkflowDesignerBin;
            foreach (var file in new[] { "SourceCode.K2Designer.dll", "SourceCode.Designer.Client.dll", "Newtonsoft.Json.dll" })
                if (!File.Exists(Path.Combine(webBin, file))) throw new CliException("Required K2 Workflow Designer assembly is missing: " + file);
            Console.WriteLine("K2 install: " + RuntimeAssemblyResolver.InstallDirectory);
            Console.WriteLine("Workflow designer: " + webBin);
            Console.WriteLine("Identity: " + WindowsIdentity.GetCurrent().Name);
            Console.WriteLine("Authoring model: K2 Five HTML5 Workflow Designer JSON");
            Console.WriteLine("Designer environment: smartforms");
        }

        public string Render()
        {
            string json;
            if (string.Equals(_manifest.Workflow.Kind, "json-file", StringComparison.OrdinalIgnoreCase))
            {
                var path = Path.GetFullPath(Path.Combine(_manifest.ManifestDirectory, _manifest.Workflow.DefinitionFile));
                if (!File.Exists(path)) throw new CliException("Workflow definition file not found: " + path);
                json = File.ReadAllText(path);
            }
            else json = WorkflowJsonBuilder.BuildStartEnd(_manifest.Workflow.Name);
            return WorkflowJsonBuilder.ParseAndValidate(json, _manifest.Workflow.Name).ToString(Formatting.None);
        }

        public void Plan()
        {
            var existing = GetProcessId();
            Console.WriteLine("Workflow: " + _manifest.Workflow.ProcessFullName);
            Console.WriteLine("Category: " + _manifest.Application.WorkflowsCategoryPath);
            Console.WriteLine("Definition: " + _manifest.Workflow.Kind);
            Console.WriteLine("Action: " + (existing.HasValue ? "update JSON process " + existing.Value : "create JSON process"));
            Console.WriteLine("Publish: " + _manifest.Workflow.Publish);
            Console.WriteLine("Rendered JSON bytes: " + System.Text.Encoding.UTF8.GetByteCount(Render()));
        }

        public void Deploy()
        {
            var categoryId = EnsureWorkflowsCategory();
            var existing = GetProcessId();
            if (existing.HasValue && !_manifest.Workflow.ReplaceExisting)
                throw new CliException("Workflow already exists. Set workflow.replaceExisting to true to update it: " + _manifest.Workflow.ProcessFullName);
            var processId = existing ?? 0;
            var jsonId = Guid.NewGuid().ToString();
            if (existing.HasValue)
            {
                var info = GetProcessInfo(existing.Value);
                var property = info.GetType().GetProperty("JsonId");
                if (property != null && property.GetValue(info, null) != null) jsonId = Convert.ToString(property.GetValue(info, null));
            }
            var response = Invoke("SaveKprx", _manifest.K2.DesignerHost, _userName, processId, jsonId,
                _manifest.Workflow.ProcessFullName, Render(), categoryId.ToString(), true,
                string.IsNullOrWhiteSpace(_manifest.Workflow.Comment) ? "Published by k2wf" : _manifest.Workflow.Comment,
                _manifest.Workflow.Publish, true, Guid.NewGuid(), true, true);
            var result = JObject.Parse(Convert.ToString(response));
            if ((bool?)result["Success"] != true)
            {
                var errors = result["Errors"] == null ? result.ToString(Formatting.None) : string.Join("; ", result["Errors"].Values<string>().ToArray());
                throw new CliException("K2 rejected the workflow: " + errors);
            }
            Console.WriteLine((_manifest.Workflow.Publish ? "Published" : "Saved draft") + ": " + _manifest.Workflow.ProcessFullName);
            Console.WriteLine("JSON process ID: " + Convert.ToString(result["SavedId"]));
            if (result["ProcID"] != null) Console.WriteLine("Runtime process ID: " + Convert.ToString(result["ProcID"]));
            if (result["VersionNumber"] != null) Console.WriteLine("Runtime version: " + Convert.ToString(result["VersionNumber"]));
        }

        public void Inspect()
        {
            var id = GetProcessId();
            if (!id.HasValue) throw new CliException("Workflow not found: " + _manifest.Workflow.ProcessFullName);
            var info = GetProcessInfo(id.Value);
            var json = GetStringProperty(info, "Json");
            var root = WorkflowJsonBuilder.ParseAndValidate(json, _manifest.Workflow.Name);
            Console.WriteLine("Workflow: " + _manifest.Workflow.ProcessFullName);
            Console.WriteLine("JSON process ID: " + id.Value);
            Console.WriteLine("Designer schema version: " + GetStringProperty(info, "DesignerVersion"));
            Console.WriteLine("JSON ID: " + GetStringProperty(info, "JsonId"));
            Console.WriteLine("Nodes: " + ((JArray)root["nodes"]).Count);
            Console.WriteLine("Links: " + ((JArray)root["links"]).Count);
            Console.WriteLine("JSON bytes: " + System.Text.Encoding.UTF8.GetByteCount(json));
        }

        public void Verify()
        {
            var id = GetProcessId();
            if (!id.HasValue) throw new CliException("Saved HTML5 workflow JSON was not found.");
            var info = GetProcessInfo(id.Value);
            var root = WorkflowJsonBuilder.ParseAndValidate(GetStringProperty(info, "Json"), _manifest.Workflow.Name);
            if (!string.Equals(Convert.ToString(root["systemName"]), _manifest.Workflow.Name, StringComparison.Ordinal))
                throw new CliException("Saved workflow JSON name differs from the manifest.");
            if (_manifest.Workflow.Publish)
            {
                using (var runtime = new RuntimeWorkflowManager())
                    if (runtime.GetProcessSet(_manifest.Workflow.ProcessFullName) == null)
                        throw new CliException("Published runtime workflow was not found.");
            }
            Console.WriteLine("Verified JSON workflow: " + _manifest.Workflow.ProcessFullName + " (ID " + id.Value + ")");
            if (_manifest.Workflow.Publish) Console.WriteLine("Verified published runtime definition.");
        }

        public void Cleanup(bool deleteDeployed)
        {
            var id = GetProcessId();
            if (!deleteDeployed && _manifest.Workflow.Publish)
                throw new CliException("Cleanup of a published workflow requires --delete-deployed.");
            var runtimeExists = false;
            if (deleteDeployed)
            {
                using (var runtime = new RuntimeWorkflowManager())
                {
                    runtimeExists = runtime.GetProcessSet(_manifest.Workflow.ProcessFullName) != null;
                    if (runtimeExists)
                    {
                        var instances = runtime.GetInstanceCount(_manifest.Workflow.ProcessFullName);
                        if (instances != 0) throw new CliException("Workflow has " + instances + " runtime instance(s); cleanup will not delete instance data.");
                        runtime.DeleteAllDefinitions(_manifest.Workflow.ProcessFullName);
                    }
                }
            }
            if (id.HasValue)
            {
                var category = FindCategory(_manifest.Application.WorkflowsCategoryPath);
                if (category == null) throw new CliException("Workflows category was not found.");
                Invoke("DeleteProcessById", _manifest.K2.DesignerHost, id.Value, _userName, (int)category["id"]);
            }
            if (!id.HasValue && !runtimeExists) { Console.WriteLine("Workflow is already absent: " + _manifest.Workflow.ProcessFullName); return; }
            Console.WriteLine("Deleted workflow: " + _manifest.Workflow.ProcessFullName);
        }

        private int EnsureWorkflowsCategory()
        {
            var root = FindCategory(_manifest.Application.RootCategoryPath);
            if (root == null) throw new CliException("Application root category does not exist: " + _manifest.Application.RootCategoryPath);
            var workflows = FindCategory(_manifest.Application.WorkflowsCategoryPath);
            if (workflows != null) return (int)workflows["id"];
            var assembly = Assembly.LoadFrom(Path.Combine(RuntimeAssemblyResolver.WorkflowDesignerBin, "SourceCode.K2Designer.dll"));
            var type = assembly.GetType("SourceCode.K2Designer.Providers.Legacy.CategoryManagementProvider", true);
            var provider = Activator.CreateInstance(type, _client);
            try { type.GetMethod("CreateCategory").Invoke(provider, new object[] { "Workflows", CategorySystemId, (int)root["id"], _manifest.K2.DesignerHost }); }
            catch (TargetInvocationException ex) { throw new CliException("Unable to create Workflows category: " + ex.GetBaseException().Message); }
            finally { /* Provider shares the manager's designer client. */ }
            workflows = FindCategory(_manifest.Application.WorkflowsCategoryPath);
            if (workflows == null) throw new CliException("K2 did not return the new Workflows category.");
            Console.WriteLine("Created category: " + _manifest.Application.WorkflowsCategoryPath);
            return (int)workflows["id"];
        }

        private JObject FindCategory(string path)
        {
            var assembly = Assembly.LoadFrom(Path.Combine(RuntimeAssemblyResolver.WorkflowDesignerBin, "SourceCode.K2Designer.dll"));
            var type = assembly.GetType("SourceCode.K2Designer.Providers.Legacy.CategoryDataProvider", true);
            var provider = Activator.CreateInstance(type, _client);
            try
            {
                var normalized = path.Replace('\\', '/').Trim('/');
                var value = type.GetMethod("GetCategoryByPath").Invoke(provider, new object[] { normalized, CategorySystemId, _manifest.K2.DesignerHost });
                return JObject.Parse(Convert.ToString(value));
            }
            catch (TargetInvocationException ex)
            {
                if (ex.GetBaseException() is InvalidOperationException) return null;
                throw new CliException("Unable to read K2 category '" + path + "': " + ex.GetBaseException().Message);
            }
            finally { /* Provider shares the manager's designer client. */ }
        }

        private int? GetProcessId()
        {
            var value = Invoke("GetProcessId", _manifest.K2.DesignerHost, _manifest.Workflow.ProcessFullName);
            return value == null ? (int?)null : Convert.ToInt32(value);
        }

        private object GetProcessInfo(int id) { return Invoke("GetProcessJson", id); }
        private object Invoke(string name, params object[] args)
        {
            try
            {
                var methods = _clientType.GetMethods().Where(x => x.Name == name && x.GetParameters().Length == args.Length).ToArray();
                if (methods.Length == 0) throw new CliException("K2 designer client method is unavailable: " + name);
                return methods[0].Invoke(_client, args);
            }
            catch (TargetInvocationException ex) { throw new CliException(ex.GetBaseException().Message); }
        }

        private static object CreateDesignerClient()
        {
            var assembly = Assembly.LoadFrom(Path.Combine(RuntimeAssemblyResolver.WorkflowDesignerBin, "SourceCode.K2Designer.dll"));
            var type = assembly.GetType("SourceCode.K2Designer.ProcessBase.ConnectionClassContext", true);
            var context = Activator.CreateInstance(type);
            var method = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(x => x.Name == "GetBaseApi" && !x.IsGenericMethod && x.GetParameters().Length == 0);
            return method.Invoke(context, null);
        }

        private static string GetStringProperty(object value, string propertyName)
        {
            var property = value.GetType().GetProperty(propertyName);
            return property == null ? string.Empty : Convert.ToString(property.GetValue(value, null));
        }

        public void Dispose() { var disposable = _client as IDisposable; if (disposable != null) disposable.Dispose(); }
    }

    internal sealed class RuntimeWorkflowManager : IDisposable
    {
        private readonly object _server;
        private readonly Type _type;

        public RuntimeWorkflowManager()
        {
            var assembly = Assembly.LoadFrom(Path.Combine(RuntimeAssemblyResolver.InstallDirectory, "Bin", "SourceCode.Workflow.Management.dll"));
            _type = assembly.GetType("SourceCode.Workflow.Management.WorkflowManagementServer", true);
            _server = Activator.CreateInstance(_type, new object[] { "localhost", (uint)5555 });
            _type.GetMethod("Open", Type.EmptyTypes).Invoke(_server, null);
        }

        public object GetProcessSet(string fullName)
        {
            try { return _type.GetMethod("GetProcSet", new[] { typeof(string) }).Invoke(_server, new object[] { fullName }); }
            catch (TargetInvocationException ex)
            {
                var message = ex.GetBaseException().Message;
                if (message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0) return null;
                throw new CliException(message);
            }
        }

        public int GetInstanceCount(string fullName)
        {
            var method = _type.GetMethod("GetProcessInstancesAll", new[] { typeof(string), typeof(string), typeof(string) });
            var instances = method.Invoke(_server, new object[] { fullName, string.Empty, string.Empty });
            var count = instances.GetType().GetProperty("Count");
            return count == null ? 0 : Convert.ToInt32(count.GetValue(instances, null));
        }

        public void DeleteAllDefinitions(string fullName)
        {
            for (var i = 0; i < 100; i++)
            {
                var processSet = GetProcessSet(fullName);
                if (processSet == null) return;
                var version = Convert.ToInt32(processSet.GetType().GetProperty("ProcVersion").GetValue(processSet, null));
                _type.GetMethod("SetDefaultProcess", new[] { typeof(string), typeof(int) }).Invoke(_server, new object[] { fullName, 0 });
                var deleted = _type.GetMethod("DeleteProcessDefinition", new[] { typeof(string), typeof(int), typeof(bool) })
                    .Invoke(_server, new object[] { fullName, version, false });
                if (!Convert.ToBoolean(deleted)) throw new CliException("K2 did not delete runtime version " + version + ".");
            }
            throw new CliException("Runtime workflow has more than 100 versions; cleanup stopped.");
        }

        public void Dispose() { var disposable = _server as IDisposable; if (disposable != null) disposable.Dispose(); }
    }
}
