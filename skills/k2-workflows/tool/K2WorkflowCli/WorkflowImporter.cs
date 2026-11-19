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

        public static void ImportDraft(WorkflowManifest manifest, string explicitImporter)
        {
            ValidateManifest(manifest);
            Guid importerId;
            var selection = explicitImporter ?? Environment.GetEnvironmentVariable(ImporterEnvironmentVariable);
            if (!Guid.TryParse(selection, out importerId) || importerId == Guid.Empty)
                throw new CliException("Specify the dedicated Workflow Importer SmartObject GUID with --importer or select it in the K2 environment profile.");

            var definition = new WorkflowRenderer(manifest).Render();
            var server = new SmartObjectClientServer();
            try
            {
                server.CreateConnection();
                server.Connection.Open(K2Connection.BuildManagementConnectionString(manifest.K2));
                K2Connection.AssertAuthenticated(server.Connection, manifest.K2, "K2 Workflow Importer");
                var smartObject = server.GetSmartObject(importerId);
                var method = smartObject.AllMethods.SingleOrDefault(x => x.Name == "ConvertWorkflow");
                if (method == null)
                    throw new CliException("The selected SmartObject has no ConvertWorkflow method.");
                var file = smartObject.Properties["File"] as SmartFileProperty;
                if (file == null)
                    throw new CliException("The selected SmartObject has no File attachment property.");
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
            }
            catch (SmartObjectException ex)
            {
                var messages = ex.BrokerData == null ? string.Empty : string.Join(" ", ex.BrokerData
                    .Cast<SmartObjectExceptionData>().Select(x => x.Message).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
                throw new CliException("Workflow Importer call failed. Verify the target in Designer before retrying. " +
                    (string.IsNullOrEmpty(messages) ? ex.Message : messages));
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

        private static void ValidateManifest(WorkflowManifest manifest)
        {
            if (manifest.Workflow.Publish)
                throw new CliException("The Workflow Importer saves a Designer draft. Set workflow.publish to false for import-draft.");
            if (manifest.Workflow.SmartForms != null)
                throw new CliException("Configure SmartForms integration in Designer after import. Remove workflow.smartForms for import-draft.");
            if (string.Equals(manifest.Workflow.Kind, "stage-router", StringComparison.OrdinalIgnoreCase))
                throw new CliException("stage-router requires Designer-side validation of deployed child workflow IDs and fields.");
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
