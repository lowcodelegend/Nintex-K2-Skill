using System;
using System.IO;
using System.Globalization;
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
        private SmartObjectDescriptor _smartObject;
        private SmartObjectMethodDescriptor _approvalMatrix;
        private SmartFormsIntegrationDescriptor _smartForm;

        private static JToken At(JToken token, params object[] path)
        {
            foreach (var part in path)
            {
                if (token == null) return null;
                token = part is int ? token[(int)part] : token[Convert.ToString(part)];
            }
            return token;
        }

        public WorkflowManager(WorkflowManifest manifest)
        {
            _manifest = manifest;
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            System.Threading.Thread.CurrentPrincipal = principal;
            _userName = ResolveDesignerUser(principal);
            _client = CreateDesignerClient();
            _clientType = _client.GetType();
        }

        public static void Doctor()
        {
            var webBin = RuntimeAssemblyResolver.WorkflowDesignerBin;
            foreach (var file in new[] { "SourceCode.K2Designer.dll", "SourceCode.Designer.Client.dll", "Newtonsoft.Json.dll" })
                if (!File.Exists(Path.Combine(webBin, file))) throw new CliException("Required K2 Workflow Designer assembly is missing: " + file);
            foreach (var file in new[] { "SourceCode.Framework.dll", "SourceCode.HostClientAPI.dll", "SourceCode.SmartObjects.Client.dll" })
                if (!File.Exists(Path.Combine(RuntimeAssemblyResolver.InstallDirectory, "Bin", file))) throw new CliException("Required K2 assembly is missing: " + file);
            Console.WriteLine("K2 install: " + RuntimeAssemblyResolver.InstallDirectory);
            Console.WriteLine("Workflow designer: " + webBin);
            Console.WriteLine("Identity: " + WindowsIdentity.GetCurrent().Name);
            Console.WriteLine("Designer identity: K2:" + WindowsIdentity.GetCurrent().Name);
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
            else if (string.Equals(_manifest.Workflow.Kind, "request-approval", StringComparison.OrdinalIgnoreCase))
            {
                if (_smartObject == null) _smartObject = SmartObjectMetadata.Load(_manifest.K2, _manifest.Workflow.RequestStatusUpdate);
                if (_manifest.Workflow.ApprovalMatrix != null && _approvalMatrix == null)
                    _approvalMatrix = SmartObjectMetadata.LoadMethod(_manifest.K2, _manifest.Workflow.ApprovalMatrix.SmartObject, _manifest.Workflow.ApprovalMatrix.Method);
                if (_manifest.Workflow.SmartForms != null && _smartForm == null)
                    _smartForm = new SmartFormsIntegrationManager(_client, _manifest.K2.DesignerHost, _manifest.K2).Load(_manifest.Workflow);
                json = WorkflowJsonBuilder.BuildRequestApproval(_manifest.Workflow, _smartObject, _smartForm, _approvalMatrix);
            }
            else json = WorkflowJsonBuilder.BuildStartEnd(_manifest.Workflow.Name);
            return WorkflowJsonBuilder.ParseAndValidate(json, _manifest.Workflow.Name).ToString(Formatting.None);
        }

        public void Plan()
        {
            var existing = GetProcessId();
            Console.WriteLine("Workflow: " + _manifest.Workflow.ProcessFullName);
            Console.WriteLine("Category: " + _manifest.Application.WorkflowCategoryPath);
            Console.WriteLine("Definition: " + _manifest.Workflow.Kind);
            Console.WriteLine("Action: " + (existing.HasValue ? "update JSON process " + existing.Value : "create JSON process"));
            Console.WriteLine("Publish: " + _manifest.Workflow.Publish);
            ReportTaskAssignmentPolicy();
            if (_manifest.Workflow.ApprovalMatrix != null)
            {
                Console.WriteLine("Approval matrix: " + _manifest.Workflow.ApprovalMatrix.MatrixCode + " via " + _manifest.Workflow.ApprovalMatrix.SmartObject + "." + _manifest.Workflow.ApprovalMatrix.Method);
                Console.WriteLine("Approval matrix amount: request." + _manifest.Workflow.ApprovalMatrix.AmountProperty);
                foreach (var dimension in _manifest.Workflow.ApprovalMatrix.Dimensions)
                    Console.WriteLine("Approval matrix dimension: request." + dimension.RequestProperty + " -> " + dimension.InputProperty);
            }
            if (_manifest.Workflow.SmartForms != null)
            {
                Console.WriteLine("SmartForm: " + _manifest.Workflow.SmartForms.Form);
                Console.WriteLine("Start state: " + _manifest.Workflow.SmartForms.StartState + " (default: " + _manifest.Workflow.SmartForms.MakeStartStateDefault + ")");
                Console.WriteLine("Task state: " + _manifest.Workflow.SmartForms.TaskState);
            }
            Console.WriteLine("Rendered JSON bytes: " + System.Text.Encoding.UTF8.GetByteCount(Render()));
        }

        public void Deploy()
        {
            ReportTaskAssignmentPolicy();
            var categoryId = EnsureWorkflowCategory();
            var existing = GetProcessId();
            if (existing.HasValue && !_manifest.Workflow.ReplaceExisting)
                throw new CliException("Workflow already exists. Set workflow.replaceExisting to true to update it: " + _manifest.Workflow.ProcessFullName);
            var processId = existing ?? 0;

            // SaveKprx can check out an integrated form as part of publishing. Reconcile an existing
            // workflow's tool-owned integration first so the management provider does not race that lock.
            if (existing.HasValue && _manifest.Workflow.SmartForms != null)
            {
                if (_smartForm == null) _smartForm = new SmartFormsIntegrationManager(_client, _manifest.K2.DesignerHost, _manifest.K2).Load(_manifest.Workflow);
                new SmartFormsIntegrationManager(_client, _manifest.K2.DesignerHost, _manifest.K2).Integrate(_manifest.Workflow, _smartForm);
            }

            var jsonId = Guid.NewGuid().ToString();
            if (existing.HasValue)
            {
                var info = GetProcessInfo(existing.Value);
                var property = info.GetType().GetProperty("JsonId");
                if (property != null && property.GetValue(info, null) != null) jsonId = Convert.ToString(property.GetValue(info, null));
            }
            var clientIdentifier = Guid.NewGuid();
            var response = Invoke("SaveKprx", _manifest.K2.DesignerHost, _userName, processId, jsonId,
                _manifest.Workflow.ProcessFullName, Render(), categoryId.ToString(), true,
                string.IsNullOrWhiteSpace(_manifest.Workflow.Comment) ? "Published by k2wf" : _manifest.Workflow.Comment,
                _manifest.Workflow.Publish, true, clientIdentifier, true, true);
            var result = JObject.Parse(Convert.ToString(response));
            if ((bool?)result["Success"] != true)
            {
                var errors = result["Errors"] == null ? result.ToString(Formatting.None) :
                    string.Join("; ", result["Errors"].Children().Select(x => x.Type == JTokenType.String ? Convert.ToString(x) : x.ToString(Formatting.None)).ToArray());
                throw new CliException("K2 rejected the workflow: " + errors);
            }
            Console.WriteLine((_manifest.Workflow.Publish ? "Published" : "Saved draft") + ": " + _manifest.Workflow.ProcessFullName);
            Console.WriteLine("JSON process ID: " + Convert.ToString(result["SavedId"]));
            if (result["ProcID"] != null) Console.WriteLine("Runtime process ID: " + Convert.ToString(result["ProcID"]));
            if (result["VersionNumber"] != null) Console.WriteLine("Runtime version: " + Convert.ToString(result["VersionNumber"]));
            var savedId = Convert.ToInt32(result["SavedId"]);
            try
            {
                if (_manifest.Workflow.SmartForms != null)
                {
                    if (_smartForm == null) _smartForm = new SmartFormsIntegrationManager(_client, _manifest.K2.DesignerHost, _manifest.K2).Load(_manifest.Workflow);
                    var integration = new SmartFormsIntegrationManager(_client, _manifest.K2.DesignerHost, _manifest.K2);
                    integration.Integrate(_manifest.Workflow, _smartForm);
                    integration.CheckInAfterIntegration(_smartForm);
                }
            }
            finally
            {
                Unlock(savedId);
                Console.WriteLine("Released designer lock: " + _manifest.Workflow.ProcessFullName);
            }
        }

        public void Unlock()
        {
            var id = GetProcessId();
            if (!id.HasValue) throw new CliException("Workflow not found: " + _manifest.Workflow.ProcessFullName);
            Unlock(id.Value);
            Console.WriteLine("Released designer lock: " + _manifest.Workflow.ProcessFullName);
        }

        public void Inspect()
        {
            var id = GetProcessId();
            if (!id.HasValue) throw new CliException("Workflow not found: " + _manifest.Workflow.ProcessFullName);
            var info = GetProcessInfo(id.Value);
            var json = GetStringProperty(info, "Json");
            var root = WorkflowJsonBuilder.ParseAndValidate(json, _manifest.Workflow.Name);
            Console.WriteLine("Workflow: " + _manifest.Workflow.ProcessFullName);
            var category = FindCategory(_manifest.Application.WorkflowCategoryPath);
            if (category != null) Console.WriteLine("Category ID: " + Convert.ToString(category["id"]));
            Console.WriteLine("JSON process ID: " + id.Value);
            Console.WriteLine("Designer schema version: " + GetStringProperty(info, "DesignerVersion"));
            Console.WriteLine("JSON ID: " + GetStringProperty(info, "JsonId"));
            Console.WriteLine("Nodes: " + ((JArray)root["nodes"]).Count);
            Console.WriteLine("Links: " + ((JArray)root["links"]).Count);
            var eventTypes = ((JArray)root["nodes"]).OfType<JObject>()
                .SelectMany(x => (x["children"] as JArray) == null ? Enumerable.Empty<JObject>() : ((JArray)x["children"]).OfType<JObject>())
                .Select(x => Convert.ToString(x["componentId"])).ToArray();
            if (eventTypes.Length > 0) Console.WriteLine("Event components: " + string.Join(", ", eventTypes));
            Console.WriteLine("JSON bytes: " + System.Text.Encoding.UTF8.GetByteCount(json));
        }

        public void Export(string outputPath)
        {
            var id = GetProcessId();
            if (!id.HasValue) throw new CliException("Workflow not found: " + _manifest.Workflow.ProcessFullName);
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, GetStringProperty(GetProcessInfo(id.Value), "Json"));
            Console.WriteLine("Exported workflow JSON: " + outputPath);
        }

        public void Verify()
        {
            var id = GetProcessId();
            if (!id.HasValue) throw new CliException("Saved HTML5 workflow JSON was not found.");
            var info = GetProcessInfo(id.Value);
            var root = WorkflowJsonBuilder.ParseAndValidate(GetStringProperty(info, "Json"), _manifest.Workflow.Name);
            if (!string.Equals(Convert.ToString(root["systemName"]), _manifest.Workflow.Name, StringComparison.Ordinal))
                throw new CliException("Saved workflow JSON name differs from the manifest.");
            if (string.Equals(_manifest.Workflow.Kind, "request-approval", StringComparison.OrdinalIgnoreCase))
            {
                var components = ((JArray)root["nodes"]).OfType<JObject>()
                    .SelectMany(x => (x["children"] as JArray) == null ? Enumerable.Empty<JObject>() : ((JArray)x["children"]).OfType<JObject>())
                    .Select(x => (int?)x["componentId"]).ToArray();
                foreach (var required in new[] { 30011, 30004, 30009 })
                    if (!components.Contains(required)) throw new CliException("Saved request-approval workflow is missing event component " + required + ".");
                if (_manifest.Workflow.ApprovalMatrix == null) VerifyOriginatorTaskAssignment(root);
                else VerifyApprovalMatrixTaskAssignment(root);
                var linkPaths = ((JArray)root["links"]).OfType<JObject>().Select(x =>
                {
                    var ui = x["ui"] as JObject;
                    return ui == null ? string.Empty : Convert.ToString(ui["path"]);
                }).ToArray();
                if (linkPaths.Any(string.IsNullOrWhiteSpace) || linkPaths.Distinct(StringComparer.Ordinal).Count() != linkPaths.Length)
                    throw new CliException("Saved request-approval workflow has missing or reused connector geometry.");
                if (_manifest.Workflow.SmartForms == null)
                {
                    var fields = root["configuration"]["dataFields"] as JArray;
                    if (fields == null || !fields.OfType<JObject>().Any(x => string.Equals(Convert.ToString(x["title"]), _manifest.Workflow.RequestStatusUpdate.IdentifierDataField, StringComparison.Ordinal)))
                        throw new CliException("Saved workflow is missing the request identifier data field.");
                }
                else
                {
                    var nodes = ((JArray)root["nodes"]).OfType<JObject>().ToArray();
                    var links = ((JArray)root["links"]).OfType<JObject>().ToArray();
                    var matrixEnabled = _manifest.Workflow.ApprovalMatrix != null;
                    if ((!matrixEnabled && (nodes.Length != 6 || links.Length != 5)) || (matrixEnabled && (nodes.Length != 8 || links.Length != 8)) ||
                        !nodes.Any(x => string.Equals(Convert.ToString(x["systemName"]), "Decision", StringComparison.Ordinal)))
                        throw new CliException("Saved SmartForms request-approval workflow does not have the expected decision topology.");
                    if (components.Count(x => x == 30011) != (matrixEnabled ? 4 : 3) || components.Count(x => x == 30004) != 3 || components.Count(x => x == 30009) != 1)
                        throw new CliException("Saved SmartForms request-approval workflow does not contain the expected status, resolver, email, and task events.");
                    if (_smartObject == null) _smartObject = SmartObjectMetadata.Load(_manifest.K2, _manifest.Workflow.RequestStatusUpdate);
                    var statusEvents = nodes.SelectMany(x => (x["children"] as JArray) == null ? Enumerable.Empty<JObject>() : ((JArray)x["children"]).OfType<JObject>())
                        .Where(x => (int?)x["componentId"] == 30011 && (_manifest.Workflow.ApprovalMatrix == null || !string.Equals(Convert.ToString(x["title"]), _manifest.Workflow.ApprovalMatrix.Name, StringComparison.Ordinal))).ToArray();
                    foreach (var statusEvent in statusEvents)
                    {
                        var controls = At(statusEvent, "configuration", "controlValues") as JObject;
                        var mappings = At(controls, "pmInputs", "values") as JObject;
                        var identifierMapping = mappings == null ? null : mappings[_smartObject.Identifier.SystemName] as JObject;
                        var statusMapping = mappings == null ? null : mappings[_smartObject.Status.SystemName] as JObject;
                        if (controls == null || mappings == null || identifierMapping == null || statusMapping == null)
                            throw new CliException("A status update is not visibly mapped to both request identifier and Status properties.");
                        if (!string.Equals(Convert.ToString(At(controls, "SmartObject", "value", "smartFields", 0, "text")), "radNoOutputs", StringComparison.Ordinal) ||
                            !string.Equals(Convert.ToString(At(controls, "spSmartObject", "value", "smartFields", 0, "title")), _smartObject.DisplayName, StringComparison.Ordinal) ||
                            !string.Equals(Convert.ToString(At(controls, "cbbMethods", "value", "smartFields", 0, "title")), _smartObject.MethodDisplayName, StringComparison.Ordinal))
                            throw new CliException("A status update cannot be rehydrated by the SmartObject Method configuration panel.");
                        if (!Convert.ToString(identifierMapping["tokenReference"]).EndsWith("[{\"internalId\":" + _smartObject.Identifier.InternalId + "}]", StringComparison.Ordinal) ||
                            !Convert.ToString(statusMapping["tokenReference"]).EndsWith("[{\"internalId\":" + _smartObject.Status.InternalId + "}]", StringComparison.Ordinal))
                            throw new CliException("A status update property mapper targets the wrong SmartObject Update inputs.");
                        var wizardMappings = At(statusEvent, "wizardDefinition", "smartObjectMappings") as JArray;
                        var serviceReference = "root.externalReferenceDefinitions[{\"internalId\":2}]";
                        var serviceMappings = wizardMappings == null ? new JObject[0] : wizardMappings.OfType<JObject>().Where(x =>
                        {
                            var methodName = Convert.ToString(At(x, "methods", 0, "name"));
                            return methodName.StartsWith("GetSmartObject", StringComparison.Ordinal) || string.Equals(methodName, "GetDefaultLoadMethod", StringComparison.Ordinal);
                        }).ToArray();
                        if (serviceMappings.Length != 4 || serviceMappings.Any(x => !string.Equals(Convert.ToString(x["smartObjectName"]), serviceReference, StringComparison.Ordinal)))
                            throw new CliException("A status update wizard does not target the SmartObject Service Functions reference, so the Designer cannot load input properties.");
                    }
                    if (matrixEnabled) VerifyApprovalMatrixResolver(root);
                    var taskEvent = nodes.SelectMany(x => (x["children"] as JArray) == null ? Enumerable.Empty<JObject>() : ((JArray)x["children"]).OfType<JObject>())
                        .Single(x => (int?)x["componentId"] == 30009);
                    if (_manifest.Workflow.UserTask.Notification != null && _manifest.Workflow.UserTask.Notification.Enabled)
                    {
                        var taskConfiguration = taskEvent["configuration"] as JObject;
                        var emailConfiguration = At(taskConfiguration, "emailConfiguration") as JObject;
                        var subjectFields = At(emailConfiguration, "subject", "smartFields") as JArray;
                        var bodyFields = At(emailConfiguration, "body", "smartFields") as JArray;
                        if ((bool?)At(taskConfiguration, "sendNotification") != true ||
                            subjectFields == null || subjectFields.Count == 0 || bodyFields == null || bodyFields.Count == 0)
                            throw new CliException("The User Task notification is not enabled with configured subject and body content.");
                        if (_manifest.Workflow.UserTask.Notification.RichText && (bool?)At(emailConfiguration, "body", "richText") != true)
                            throw new CliException("The User Task notification body is not configured as rich text.");
                    }
                    var branchTitles = links.Select(x => Convert.ToString(x["title"])).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                    if (!branchTitles.Contains("Approved") || !branchTitles.Contains("Rejected"))
                        throw new CliException("Saved SmartForms request-approval workflow is missing Approved/Rejected decision branches.");
                    var items = root["configuration"]["itemReferences"] as JArray;
                    if (items == null || !items.OfType<JObject>().Any(x => (bool?)x["primary"] == true))
                        throw new CliException("Saved workflow is missing its primary SmartForms item reference.");
                    if (_smartForm == null) _smartForm = new SmartFormsIntegrationManager(_client, _manifest.K2.DesignerHost, _manifest.K2).Load(_manifest.Workflow);
                    new SmartFormsIntegrationManager(_client, _manifest.K2.DesignerHost, _manifest.K2).Verify(_manifest.Workflow, _smartForm);
                    Console.WriteLine("Verified SmartForms Start and Task rules: " + _smartForm.DisplayName);
                }
                var references = root["externalReferenceDefinitions"] as JArray;
                if (references == null || !references.OfType<JObject>().Any(x => string.Equals(Convert.ToString(x["systemName"]), _manifest.Workflow.RequestStatusUpdate.SmartObject, StringComparison.OrdinalIgnoreCase)))
                    throw new CliException("Saved workflow is missing the request SmartObject reference.");
                if (_manifest.Workflow.ApprovalMatrix != null && !references.OfType<JObject>().Any(x => string.Equals(Convert.ToString(x["systemName"]), _manifest.Workflow.ApprovalMatrix.SmartObject, StringComparison.OrdinalIgnoreCase)))
                    throw new CliException("Saved workflow is missing the approval matrix resolver SmartObject reference.");
            }
            if (_manifest.Workflow.Publish)
            {
                using (var runtime = new RuntimeWorkflowManager())
                    if (runtime.GetProcessSet(_manifest.Workflow.ProcessFullName) == null)
                        throw new CliException("Published runtime workflow was not found.");
            }
            Console.WriteLine("Verified JSON workflow: " + _manifest.Workflow.ProcessFullName + " (ID " + id.Value + ")");
            if (_manifest.Workflow.Publish) Console.WriteLine("Verified published runtime definition.");
            ReportTaskAssignmentPolicy();
        }

        public void ReportTaskAssignmentPolicy()
        {
            if (!string.Equals(_manifest.Workflow.Kind, "request-approval", StringComparison.OrdinalIgnoreCase) || _manifest.Workflow.UserTask == null) return;
            var requested = _manifest.Workflow.UserTask.Assignees == null ? new string[0] : _manifest.Workflow.UserTask.Assignees.ToArray();
            Console.WriteLine("User Task requested assignees: " + (requested.Length == 0 ? "(not supplied)" : string.Join(", ", requested)));
            if (_manifest.Workflow.ApprovalMatrix != null)
            {
                Console.WriteLine("User Task effective assignee: approval matrix " + _manifest.Workflow.ApprovalMatrix.MatrixCode + "." + _manifest.Workflow.ApprovalMatrix.ApproverProperty);
                Console.WriteLine("ERRATA [approval-matrix-demo-identities]: Generated matrix rows use the designing K2 identity for testing/demo and must be reviewed before production.");
                return;
            }
            Console.WriteLine("User Task effective assignee: $originator (ProcessOriginatorFQN)");
            Console.WriteLine("ERRATA [test-demo-routing]: Human task assignment is forced to the workflow Originator for testing/demo; requested production routing is not applied.");
        }

        private static void VerifyOriginatorTaskAssignment(JObject root)
        {
            var task = ((JArray)root["nodes"]).OfType<JObject>()
                .SelectMany(x => (x["children"] as JArray) == null ? Enumerable.Empty<JObject>() : ((JArray)x["children"]).OfType<JObject>())
                .Single(x => (int?)x["componentId"] == 30009);
            var destinations = At(task, "configuration", "destinationSets") as JArray;
            var firstSet = destinations == null ? null : destinations.OfType<JObject>().SingleOrDefault();
            var items = firstSet == null ? null : firstSet["destinationItems"] as JArray;
            var item = items == null ? null : items.OfType<JObject>().SingleOrDefault();
            if (firstSet == null || item == null ||
                !string.Equals(Convert.ToString(firstSet["title"]), "Originator", StringComparison.Ordinal) ||
                !string.Equals(Convert.ToString(At(item, "smartFields", 0, "fieldName")), "ProcessOriginatorFQN", StringComparison.Ordinal))
                throw new CliException("The generated User Task is not assigned exclusively to the workflow Originator test/demo identity.");
            Console.WriteLine("Verified User Task effective assignee: Originator (ProcessOriginatorFQN)");
        }

        private static void VerifyApprovalMatrixTaskAssignment(JObject root)
        {
            var task = ((JArray)root["nodes"]).OfType<JObject>()
                .SelectMany(x => (x["children"] as JArray) == null ? Enumerable.Empty<JObject>() : ((JArray)x["children"]).OfType<JObject>())
                .Single(x => (int?)x["componentId"] == 30009);
            var destination = At(task, "configuration", "destinationSets", 0) as JObject;
            var item = At(destination, "destinationItems", 0) as JObject;
            if (destination == null || item == null ||
                !string.Equals(Convert.ToString(destination["title"]), "Approval Matrix Approver", StringComparison.Ordinal) ||
                !Convert.ToString(At(item, "smartFields", 0, "dataFieldReference")).EndsWith("[{\"internalId\":3}]", StringComparison.Ordinal))
                throw new CliException("The generated User Task is not assigned to the approval matrix Approver data field.");
            Console.WriteLine("Verified User Task effective assignee: approval matrix Approver data field");
        }

        private void VerifyApprovalMatrixResolver(JObject root)
        {
            if (_approvalMatrix == null) _approvalMatrix = SmartObjectMetadata.LoadMethod(_manifest.K2, _manifest.Workflow.ApprovalMatrix.SmartObject, _manifest.Workflow.ApprovalMatrix.Method);
            var resolver = ((JArray)root["nodes"]).OfType<JObject>()
                .SelectMany(x => (x["children"] as JArray) == null ? Enumerable.Empty<JObject>() : ((JArray)x["children"]).OfType<JObject>())
                .Single(x => (int?)x["componentId"] == 30011 && string.Equals(Convert.ToString(x["title"]), _manifest.Workflow.ApprovalMatrix.Name, StringComparison.Ordinal));
            var outputs = At(resolver, "configuration", "controlValues", "pmOutputs", "values") as JObject;
            if (outputs == null) throw new CliException("Approval matrix resolver has no output mappings.");
            foreach (var propertyName in new[] { _manifest.Workflow.ApprovalMatrix.HasApproverProperty, _manifest.Workflow.ApprovalMatrix.StageProperty, _manifest.Workflow.ApprovalMatrix.ApproverProperty, _manifest.Workflow.ApprovalMatrix.ApproverTypeProperty })
            {
                var property = _approvalMatrix.Returns.Single(x => string.Equals(x.SystemName, propertyName, StringComparison.OrdinalIgnoreCase));
                var mapping = outputs[property.SystemName] as JObject;
                if (mapping == null || !Convert.ToString(mapping["tokenReference"]).EndsWith("[{\"internalId\":" + property.InternalId + "}]", StringComparison.Ordinal))
                    throw new CliException("Approval matrix resolver output is not mapped: " + propertyName);
            }
            Console.WriteLine("Verified approval matrix resolver mappings and multi-stage loop: " + _manifest.Workflow.ApprovalMatrix.MatrixCode);
        }

        public void Cleanup(bool deleteDeployed, bool deferSmartFormsIntegration)
        {
            var id = GetProcessId();
            if (!deleteDeployed && _manifest.Workflow.Publish)
                throw new CliException("Cleanup of a published workflow requires --delete-deployed.");
            if (deferSmartFormsIntegration && _manifest.Workflow.SmartForms == null)
                throw new CliException("--defer-smartforms-integration requires workflow.smartForms.");
            if (deferSmartFormsIntegration && !deleteDeployed)
                throw new CliException("--defer-smartforms-integration requires --delete-deployed.");
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
                    }
                }
            }
            if (deleteDeployed && _manifest.Workflow.SmartForms != null && !deferSmartFormsIntegration)
            {
                if (_smartForm == null) _smartForm = new SmartFormsIntegrationManager(_client, _manifest.K2.DesignerHost, _manifest.K2).LoadForCleanup(_manifest.Workflow);
                new SmartFormsIntegrationManager(_client, _manifest.K2.DesignerHost, _manifest.K2).Remove(_manifest.Workflow, _smartForm);
            }
            else if (deleteDeployed && deferSmartFormsIntegration)
                Console.WriteLine("Deferred SmartForm integration removal to manifest-owned Form deletion: " + _manifest.Workflow.SmartForms.Form);
            if (!id.HasValue && !runtimeExists) { Console.WriteLine("Workflow definitions are already absent: " + _manifest.Workflow.ProcessFullName); return; }
            if (runtimeExists)
            {
                using (var runtime = new RuntimeWorkflowManager()) runtime.DeleteAllDefinitions(_manifest.Workflow.ProcessFullName);
            }
            if (id.HasValue)
            {
                var category = FindCategory(_manifest.Application.WorkflowCategoryPath);
                var categoryId = category == null ? GetProcessCategoryId(id.Value) : (int?)category["id"];
                if (!categoryId.HasValue) throw new CliException("Workflow category was not found and the process has no recoverable category ID: " + _manifest.Application.WorkflowCategoryPath);
                if (category == null) Console.WriteLine("Workflow category link is already absent; using process category ID " + categoryId.Value + " for deletion.");
                Invoke("DeleteProcessById", _manifest.K2.DesignerHost, id.Value, _userName, categoryId.Value);
            }
            Console.WriteLine("Deleted workflow: " + _manifest.Workflow.ProcessFullName);
        }

        private int EnsureWorkflowCategory()
        {
            var root = FindCategory(_manifest.Application.RootCategoryPath);
            if (root == null) throw new CliException("Application root category does not exist: " + _manifest.Application.RootCategoryPath);
            var workflows = FindCategory(_manifest.Application.WorkflowCategoryPath);
            if (workflows != null) return (int)workflows["id"];
            var assembly = Assembly.LoadFrom(Path.Combine(RuntimeAssemblyResolver.WorkflowDesignerBin, "SourceCode.K2Designer.dll"));
            var type = assembly.GetType("SourceCode.K2Designer.Providers.Legacy.CategoryManagementProvider", true);
            var provider = Activator.CreateInstance(type, _client);
            try { type.GetMethod("CreateCategory").Invoke(provider, new object[] { _manifest.Application.WorkflowCategoryName, CategorySystemId, (int)root["id"], _manifest.K2.DesignerHost }); }
            catch (TargetInvocationException ex) { throw new CliException("Unable to create workflow category '" + _manifest.Application.WorkflowCategoryName + "': " + ex.GetBaseException().Message); }
            finally { /* Provider shares the manager's designer client. */ }
            workflows = FindCategory(_manifest.Application.WorkflowCategoryPath);
            if (workflows == null) throw new CliException("K2 did not return the new workflow category: " + _manifest.Application.WorkflowCategoryPath);
            Console.WriteLine("Created category: " + _manifest.Application.WorkflowCategoryPath);
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

        private object GetProcessInfo(int id)
        {
            var metadata = GetProcessMetadata(id);
            var version = GetStringProperty(metadata, "Version");
            if (string.IsNullOrWhiteSpace(version))
                version = GetStringProperty(metadata, "MajorNo") + "." + GetStringProperty(metadata, "MinorNo");
            object definition;
            try { definition = _clientType.GetMethod("GetProcessDefinitionPerVersion", new[] { typeof(int), typeof(string) }).Invoke(_client, new object[] { id, version }); }
            catch (TargetInvocationException ex) { throw new CliException(ex.GetBaseException().Message); }
            if (definition == null) throw new CliException("K2 did not return workflow version " + version + " for process " + id + ".");
            return definition;
        }

        private object GetProcessMetadata(int id)
        {
            try { return _clientType.GetMethod("GetProcessInfo", new[] { typeof(int) }).Invoke(_client, new object[] { id }); }
            catch (TargetInvocationException ex) { throw new CliException(ex.GetBaseException().Message); }
        }

        private int? GetProcessCategoryId(int id)
        {
            var metadata = GetProcessMetadata(id);
            foreach (var name in new[] { "CategoryId", "CategoryID", "Category" })
            {
                var property = metadata.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property == null) continue;
                int value;
                if (int.TryParse(Convert.ToString(property.GetValue(metadata, null)), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return value;
            }
            if (Environment.GetEnvironmentVariable("K2WF_DEBUG") == "1")
                Console.WriteLine("Process metadata properties: " + string.Join(", ", metadata.GetType().GetProperties().Select(x => x.Name + "=" + Convert.ToString(x.GetValue(metadata, null))).ToArray()));
            return null;
        }
        private void Unlock(int processId)
        {
            var unlockResult = Convert.ToString(Invoke("ExecuteFrameworkMethod", _manifest.K2.DesignerHost, "Process", "Processdataservice", "UnlockProcess",
                CultureInfo.CurrentUICulture.Name, new object[] { processId, _userName }));
            int affected;
            if (!int.TryParse(unlockResult, NumberStyles.Integer, CultureInfo.InvariantCulture, out affected))
                throw new CliException("K2 rejected the workflow unlock: " + unlockResult);
        }
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

        private static string ResolveDesignerUser(IPrincipal principal)
        {
            var assembly = Assembly.LoadFrom(Path.Combine(RuntimeAssemblyResolver.WorkflowDesignerBin, "SourceCode.K2Designer.dll"));
            var type = assembly.GetType("SourceCode.K2Designer.ProcessBase.ConnectionClassContext", true);
            var context = Activator.CreateInstance(type);
            var value = Convert.ToString(type.GetMethod("GetUser").Invoke(context, new object[] { principal }));
            if (string.IsNullOrWhiteSpace(value))
            {
                var name = principal.Identity.Name;
                return name.StartsWith("K2:", StringComparison.OrdinalIgnoreCase) ? name : "K2:" + name;
            }
            return value;
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
