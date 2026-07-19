using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace K2WorkflowCli
{
    internal sealed class WorkflowManifest
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("k2")] public K2Settings K2 { get; set; }
        [JsonProperty("application")] public ApplicationSettings Application { get; set; }
        [JsonProperty("workflow")] public WorkflowSettings Workflow { get; set; }
        [JsonIgnore] public string ManifestDirectory { get; private set; }

        public static WorkflowManifest Load(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new CliException("Manifest not found: " + fullPath);
            WorkflowManifest manifest;
            try { manifest = JsonConvert.DeserializeObject<WorkflowManifest>(File.ReadAllText(fullPath)); }
            catch (Exception ex) { throw new CliException("Invalid manifest JSON: " + ex.Message); }
            if (manifest == null) throw new CliException("Manifest is empty.");
            manifest.ManifestDirectory = Path.GetDirectoryName(fullPath);
            manifest.Validate();
            return manifest;
        }

        private void Validate()
        {
            if (SchemaVersion != 1) throw new CliException("schemaVersion must be 1.");
            if (K2 == null) K2 = new K2Settings();
            if (string.IsNullOrWhiteSpace(K2.DesignerHost)) K2.DesignerHost = "smartforms";
            if (!string.Equals(K2.DesignerHost, "smartforms", StringComparison.OrdinalIgnoreCase))
                throw new CliException("k2.designerHost must be 'smartforms' for the installed K2 Five HTML5 Workflow Designer environment.");
            if (Application == null || string.IsNullOrWhiteSpace(Application.RootCategoryPath))
                throw new CliException("application.rootCategoryPath is required.");
            ValidateNoVersion(Application.RootCategoryPath, "application.rootCategoryPath");
            if (Application.RootCategoryPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(x => string.Equals(x, "Workflows", StringComparison.OrdinalIgnoreCase)))
                throw new CliException("application.rootCategoryPath must be the application root; the CLI appends a Workflows subcategory.");
            if (Workflow == null || string.IsNullOrWhiteSpace(Workflow.Name)) throw new CliException("workflow.name is required.");
            ValidateNoVersion(Workflow.Name, "workflow.name");
            if (Workflow.Name.IndexOfAny(new[] { '\\', '/' }) >= 0) throw new CliException("workflow.name must be a leaf name.");
            if (string.IsNullOrWhiteSpace(Workflow.Kind)) Workflow.Kind = "start-end";
            if (!string.Equals(Workflow.Kind, "start-end", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Workflow.Kind, "json-file", StringComparison.OrdinalIgnoreCase))
                throw new CliException("workflow.kind must be 'start-end' or 'json-file'.");
            if (string.Equals(Workflow.Kind, "json-file", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Workflow.DefinitionFile))
                throw new CliException("workflow.definitionFile is required when workflow.kind is 'json-file'.");
        }

        private static void ValidateNoVersion(string value, string field)
        {
            if (Regex.IsMatch(value, @"(^|[\\/\s_-])v?\d+\.\d+(?:\.\d+)?($|[\\/\s_-])", RegexOptions.IgnoreCase))
                throw new CliException(field + " must not contain a version. K2 versions workflows internally.");
        }
    }

    internal sealed class K2Settings
    {
        [JsonProperty("designerHost")] public string DesignerHost { get; set; }
    }

    internal sealed class ApplicationSettings
    {
        [JsonProperty("rootCategoryPath")] public string RootCategoryPath { get; set; }
        [JsonIgnore] public string WorkflowsCategoryPath { get { return RootCategoryPath.TrimEnd('\\', '/') + "\\Workflows"; } }
    }

    internal sealed class WorkflowSettings
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("definitionFile")] public string DefinitionFile { get; set; }
        [JsonProperty("publish")] public bool Publish { get; set; }
        [JsonProperty("replaceExisting")] public bool ReplaceExisting { get; set; }
        [JsonProperty("comment")] public string Comment { get; set; }
        [JsonIgnore] public string ProcessFullName { get { return "Workflows\\" + Name; } }
    }
}
