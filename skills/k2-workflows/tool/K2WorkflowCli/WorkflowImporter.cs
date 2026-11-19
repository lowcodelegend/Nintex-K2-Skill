using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SourceCode.Categories.Client;
using SourceCode.SmartObjects.Client;

namespace K2WorkflowCli
{
    internal static class WorkflowImporter
    {
        internal const string ImporterEnvironmentVariable = "K2_WORKFLOW_IMPORTER_SMARTOBJECT_ID";

        public static void Doctor(K2Settings settings, string explicitImporter)
        {
            Console.WriteLine("Workflow authoring: K2 Workflow Import Service (JSONata draft import)");
            if (settings == null) return;

            var importerId = ResolveImporter(explicitImporter);
            var server = new SmartObjectClientServer();
            try
            {
                Connect(server, settings);
                GetImporter(server, importerId);
                Console.WriteLine("Workflow Importer SmartObject: " + importerId);
                Console.WriteLine("K2 identity: " + K2Connection.DescribeIdentity(settings));
            }
            finally { Close(server); }
        }

        public static void Plan(WorkflowManifest manifest)
        {
            ValidateManifest(manifest);
            var workflow = manifest.Workflow;
            Console.WriteLine("Workflow: " + workflow.ProcessFullName);
            Console.WriteLine("Category: " + manifest.Application.WorkflowCategoryPath);
            Console.WriteLine("Definition: " + workflow.Kind);
            Console.WriteLine("Action: import Designer draft (" + (workflow.ReplaceExisting ? "Overwrite" : "Skip existing") + ")");
            ReportDesignerSteps(manifest);
        }

        public static JObject Render(WorkflowManifest manifest)
        {
            var workflow = manifest.Workflow;
            if ((workflow.SmartForms != null && string.Equals(workflow.Kind, "request-approval", StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(workflow.Kind, "stage-router", StringComparison.OrdinalIgnoreCase))
            {
                using (var manager = new WorkflowManager(manifest))
                    return manager.Render();
            }
            return new WorkflowRenderer(manifest).Render();
        }

        public static void ImportDraft(WorkflowManifest manifest, string explicitImporter)
        {
            ValidateManifest(manifest);
            var importerId = ResolveImporter(explicitImporter);
            var definition = Render(manifest);
            var server = new SmartObjectClientServer();
            try
            {
                Connect(server, manifest.K2);
                var smartObject = GetImporter(server, importerId);
                var method = smartObject.AllMethods.Single(x => x.Name == "ConvertWorkflow");
                var file = (SmartFileProperty)smartObject.Properties["File"];
                var categoryId = EnsureCategory(manifest);
                smartObject.MethodToExecute = method.Name;
                file.FileName = "k2skills-workflow.xml";
                file.Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(CreateInputPayload(manifest.Workflow.Name, definition, categoryId)));
                smartObject.Properties["CategoryID"].Value = categoryId.ToString(CultureInfo.InvariantCulture);
                method.Parameters["Host"].Value = "smartForms";
                method.Parameters["SaveAction"].Value = manifest.Workflow.ReplaceExisting ? "Overwrite" : "Skip";

                var results = server.ExecuteList(smartObject).SmartObjectsList;
                if (results.Count != 1)
                    throw new CliException("K2 did not return exactly one import result. Verify the category in Designer before retrying.");
                ReportResult(results[0], categoryId, manifest.Workflow.ProcessFullName);
                WorkflowManager.ReportTaskAssignmentPolicy(manifest.Workflow);
                ReportDesignerSteps(manifest);
            }
            catch (SmartObjectException ex)
            {
                var messages = ex.BrokerData == null ? string.Empty : string.Join(" ", ex.BrokerData
                    .Cast<SmartObjectExceptionData>().Select(x => x.Message).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
                throw new CliException("Workflow Importer call failed. Verify the target in Designer before retrying. " +
                    (string.IsNullOrEmpty(messages) ? ex.Message : messages));
            }
            finally { Close(server); }
        }

        private static Guid ResolveImporter(string explicitImporter)
        {
            Guid id;
            var selection = explicitImporter ?? Environment.GetEnvironmentVariable(ImporterEnvironmentVariable);
            if (!Guid.TryParse(selection, out id) || id == Guid.Empty)
                throw new CliException("Configure the dedicated Workflow Importer in the K2 environment profile or pass its SmartObject GUID with --importer.");
            return id;
        }

        private static void Connect(SmartObjectClientServer server, K2Settings settings)
        {
            server.CreateConnection();
            server.Connection.Open(K2Connection.BuildManagementConnectionString(settings));
            K2Connection.AssertAuthenticated(server.Connection, settings, "K2 Workflow Importer");
        }

        private static SmartObject GetImporter(SmartObjectClientServer server, Guid id)
        {
            var smartObject = server.GetSmartObject(id);
            if (!smartObject.AllMethods.Any(x => x.Name == "ConvertWorkflow") ||
                !(smartObject.Properties["File"] is SmartFileProperty))
                throw new CliException("The selected SmartObject does not expose Workflow Importer ConvertWorkflow and its File attachment.");
            return smartObject;
        }

        private static void Close(SmartObjectClientServer server)
        {
            if (server.Connection == null) return;
            server.Connection.Close();
            server.DeleteConnection();
        }

        private static void ReportDesignerSteps(WorkflowManifest manifest)
        {
            if (manifest.Workflow.SmartForms != null)
                Console.WriteLine("Configure the declared SmartForms Start/Task integration in K2 Designer.");
            if (manifest.Workflow.Publish)
                Console.WriteLine("Publication pending: open the imported draft and use File > Deploy in K2 Designer.");
        }

        private static void ValidateManifest(WorkflowManifest manifest)
        {
            if (manifest.Workflow.Name != manifest.Workflow.Name.Trim())
                throw new CliException("workflow.name must not begin or end with whitespace.");
            if (manifest.Workflow.Name.IndexOfAny(new[] { ':', '*', '?', '"', '<', '>', ',', '|', '(', ')', '\'', ';' }) >= 0)
                throw new CliException("workflow.name contains punctuation that the Workflow Importer removes.");
        }

        internal static string CreateInputPayload(string name, JObject definition, int categoryId)
        {
            var graph = (JObject)definition.DeepClone();
            graph.Remove("componentId");
            graph.Remove("title");
            graph.Remove("systemName");
            var input = new XElement("K2SkillsWorkflow",
                new XAttribute("version", "1"),
                new XAttribute("name", EncodeText(name)),
                new XAttribute("categoryId", categoryId),
                EncodeValue("Graph", graph));

            return input.ToString(SaveOptions.DisableFormatting);
        }

        private static XElement EncodeValue(string elementName, JToken token)
        {
            var element = new XElement(elementName);
            switch (token.Type)
            {
                case JTokenType.Object:
                    element.SetAttributeValue("type", "object");
                    foreach (var property in ((JObject)token).Properties())
                    {
                        var entry = EncodeValue("Entry", property.Value);
                        entry.SetAttributeValue("key", EncodeText(property.Name));
                        element.Add(entry);
                    }
                    break;
                case JTokenType.Array:
                    element.SetAttributeValue("type", "array");
                    foreach (var child in token) element.Add(EncodeValue("Entry", child));
                    break;
                case JTokenType.String:
                    element.SetAttributeValue("type", "string");
                    element.SetAttributeValue("value", EncodeText((string)token));
                    break;
                case JTokenType.Integer:
                case JTokenType.Float:
                    element.SetAttributeValue("type", "number");
                    element.SetAttributeValue("value", token.ToString(Formatting.None));
                    break;
                case JTokenType.Boolean:
                    element.SetAttributeValue("type", "boolean");
                    element.SetAttributeValue("value", token.ToString(Formatting.None));
                    break;
                case JTokenType.Null:
                    element.SetAttributeValue("type", "null");
                    break;
                default:
                    throw new CliException("Unsupported Workflow Importer JSON value: " + token.Type);
            }
            return element;
        }

        private static string EncodeText(string text)
        {
            return System.Net.WebUtility.UrlEncode(text).Replace("+", "%20");
        }

        private static int EnsureCategory(WorkflowManifest manifest)
        {
            var server = new CategoryServer();
            try
            {
                server.CreateConnection();
                server.Connection.Open(K2Connection.BuildManagementConnectionString(manifest.K2));
                K2Connection.AssertAuthenticated(server.Connection, manifest.K2, "K2 category lookup");
                var manager = server.GetCategoryManager(1, false, true);
                var category = FindCategory(manager, manifest.Application.WorkflowCategoryPath);
                if (category != null) return category.Id;
                var parent = FindCategory(manager, manifest.Application.RootCategoryPath);
                if (parent == null)
                    throw new CliException("Application root category does not exist: " + manifest.Application.RootCategoryPath);

                var child = manager.NewCategory();
                child.Name = manifest.Application.WorkflowCategoryName;
                child.Metadata.DisplayName = child.Name;
                child.SetParentCategory(parent);
                server.AddCategory(child);
                manager = server.GetCategoryManager(1, false, true);
                category = FindCategory(manager, manifest.Application.WorkflowCategoryPath);
                if (category == null)
                    throw new CliException("K2 did not return the new workflow category: " + manifest.Application.WorkflowCategoryPath);
                return category.Id;
            }
            finally
            {
                if (server.Connection != null)
                {
                    server.Connection.Close();
                    server.DeleteConnection();
                }
            }
        }

        private static Category FindCategory(CategoryManager manager, string path)
        {
            return manager.Categories.Cast<Category>().SingleOrDefault(x => !x.IsRoot && string.Equals(
                NormalizePath((x.Path ?? "") + "\\" + x.Name),
                NormalizePath(path), StringComparison.OrdinalIgnoreCase));
        }
        private static string NormalizePath(string path)
        {
            return path.Replace('/', '\\').Trim('\\');
        }

        private static void ReportResult(SmartObject result, int categoryId, string expectedName)
        {
            var message = result.Properties["ConversionMessage"].Value;
            var success = result.Properties["Success"].Value;
            if (!string.Equals(success, "true", StringComparison.OrdinalIgnoreCase) && success != "1")
                throw new CliException("Workflow import failed: " + message);

            int id;
            var name = result.Properties["Name"].Value;
            if (!int.TryParse(result.Properties["ID"].Value, out id) || id <= 0 ||
                !string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase) ||
                result.Properties["CategoryID"].Value != categoryId.ToString(CultureInfo.InvariantCulture))
                throw new CliException("K2 did not confirm the requested draft. Verify the category in Designer before retrying. " + message);
            Console.WriteLine("Imported workflow draft: " + name);
            Console.WriteLine("JSON process ID: " + id);
        }
    }
}
