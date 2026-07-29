using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace K2SmartBoxCli
{
    public sealed class DeploymentManifest
    {
        public int SchemaVersion { get; set; }
        public string Name { get; set; }
        public ApplicationOptions Application { get; set; }
        public K2Options K2 { get; set; }
        public DeploymentOptions Deployment { get; set; }
        public List<SmartObjectDefinition> SmartObjects { get; set; }
        public VerificationOptions Verification { get; set; }
        [ScriptIgnore] public string ManifestPath { get; private set; }

        public static DeploymentManifest Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new CliException("Specify --manifest <path>.");
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new CliException("Manifest not found: " + fullPath);
            DeploymentManifest manifest;
            try
            {
                manifest = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }
                    .Deserialize<DeploymentManifest>(File.ReadAllText(fullPath));
            }
            catch (Exception ex) { throw new CliException("Invalid manifest JSON: " + ex.Message); }
            if (manifest == null) throw new CliException("Manifest is empty.");
            manifest.ManifestPath = fullPath;
            manifest.NormalizeAndValidate();
            return manifest;
        }

        private void NormalizeAndValidate()
        {
            if (SchemaVersion != 1) throw new CliException("schemaVersion must be 1.");
            Require(Name, "name");
            Application = Application ?? new ApplicationOptions();
            K2 = K2 ?? new K2Options();
            Deployment = Deployment ?? new DeploymentOptions();
            SmartObjects = SmartObjects ?? new List<SmartObjectDefinition>();
            Verification = Verification ?? new VerificationOptions();
            if (string.IsNullOrWhiteSpace(K2.Host)) K2.Host = "localhost";
            if (K2.Port == 0) K2.Port = 5555;
            if (string.IsNullOrWhiteSpace(K2.SecurityLabel)) K2.SecurityLabel = "K2";
            if (K2.Port < 1 || K2.Port > 65535) throw new CliException("k2.port must be between 1 and 65535.");
            if (!K2.Integrated)
            {
                Require(K2.UserName, "k2.userName");
                Require(K2.PasswordEnvironmentVariable, "k2.passwordEnvironmentVariable");
            }
            Require(Application.RootCategoryPath, "application.rootCategoryPath");
            Application.RootCategoryPath = Application.RootCategoryPath.Trim().TrimEnd('\\', '/');
            if (Application.RootCategoryPath.Split('\\', '/').Last().Equals("Data", StringComparison.OrdinalIgnoreCase))
                throw new CliException("application.rootCategoryPath must be the solution root, not its Data child.");
            if (SmartObjects.Count == 0) throw new CliException("smartObjects must contain at least one object.");
            var objectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in SmartObjects)
            {
                if (item == null) throw new CliException("smartObjects cannot contain null entries.");
                Require(item.SystemName, "smartObjects.systemName");
                Require(item.DisplayName, "smartObjects.displayName");
                if (!Regex.IsMatch(item.SystemName, @"^[A-Za-z][A-Za-z0-9_]*$"))
                    throw new CliException("SmartObject systemName must start with a letter and contain only letters, digits, and underscore: " + item.SystemName);
                if (!objectNames.Add(item.SystemName)) throw new CliException("Duplicate SmartObject systemName: " + item.SystemName);
                item.Properties = item.Properties ?? new List<PropertyDefinition>();
                if (item.Properties.Count == 0) throw new CliException("SmartObject has no properties: " + item.SystemName);
                var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in item.Properties)
                {
                    if (property == null) throw new CliException("SmartObject properties cannot contain null entries: " + item.SystemName);
                    Require(property.Name, item.SystemName + ".properties.name");
                    if (string.IsNullOrWhiteSpace(property.DisplayName)) property.DisplayName = property.Name;
                    if (!Regex.IsMatch(property.Name, @"^[A-Za-z][A-Za-z0-9_]*$"))
                        throw new CliException("Property names must start with a letter and contain only letters, digits, and underscore: " + item.SystemName + "." + property.Name);
                    if (!propertyNames.Add(property.Name)) throw new CliException("Duplicate property: " + item.SystemName + "." + property.Name);
                    property.Type = CanonicalType(property.Type, item.SystemName + "." + property.Name);
                    if ((property.Type == "autonumber" || property.Type == "autoguid") && (!property.Key || property.Required))
                        throw new CliException(property.Type + " must be the non-required key: " + item.SystemName + "." + property.Name);
                    if (property.Type == "text")
                    {
                        if (property.MaxLength == 0) property.MaxLength = 100;
                        if (property.MaxLength < 1 || property.MaxLength > 4000)
                            throw new CliException("Text maxLength must be between 1 and 4000: " + item.SystemName + "." + property.Name);
                    }
                    else if (property.MaxLength != 0)
                        throw new CliException("maxLength is supported only for Text: " + item.SystemName + "." + property.Name);
                }
                if (item.Properties.Count(x => x.Key) != 1)
                    throw new CliException("SmartObject must declare exactly one key property: " + item.SystemName);
            }
        }

        internal static string CanonicalType(string value, string label)
        {
            var normalized = (value ?? string.Empty).Replace("-", "").ToLowerInvariant();
            var supported = new[] { "autonumber", "autoguid", "text", "memo", "number", "yesno", "date", "datetime", "guid", "file", "image" };
            if (!supported.Contains(normalized))
                throw new CliException("Unsupported SmartBox type '" + value + "' for " + label + ". Supported: " + string.Join(", ", supported));
            return normalized;
        }

        private static void Require(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new CliException(field + " is required.");
        }

        [ScriptIgnore] public string DataCategoryPath { get { return Application.RootCategoryPath + "\\Data"; } }
    }

    public sealed class ApplicationOptions { public string RootCategoryPath { get; set; } }
    public sealed class DeploymentOptions { public bool UpdateExisting { get; set; } }
    public sealed class VerificationOptions { public bool SmokeTestLists { get; set; } }
    public sealed class K2Options
    {
        public K2Options() { Integrated = true; }
        public string Host { get; set; }
        public int Port { get; set; }
        public bool Integrated { get; set; }
        public string SecurityLabel { get; set; }
        public string Domain { get; set; }
        public string UserName { get; set; }
        public string PasswordEnvironmentVariable { get; set; }
    }
    public sealed class SmartObjectDefinition
    {
        public string SystemName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public List<PropertyDefinition> Properties { get; set; }
    }
    public sealed class PropertyDefinition
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public bool Key { get; set; }
        public bool Required { get; set; }
        public int MaxLength { get; set; }
    }
}
