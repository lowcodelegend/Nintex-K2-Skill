using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace K2SmartFormsCli
{
    public sealed class SmartFormsManifest
    {
        public string Name { get; set; }
        public K2ConnectionOptions K2 { get; set; }
        public ApplicationOptions Application { get; set; }
        public VerificationOptions Verification { get; set; }

        public SmartFormsManifest()
        {
            K2 = new K2ConnectionOptions();
            Application = new ApplicationOptions();
            Verification = new VerificationOptions();
        }

        [ScriptIgnore]
        public string ManifestPath { get; private set; }

        public static SmartFormsManifest Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new CliException("Specify --manifest <path>.");
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new CliException("Manifest not found: " + fullPath);

            SmartFormsManifest manifest;
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                manifest = serializer.Deserialize<SmartFormsManifest>(File.ReadAllText(fullPath));
            }
            catch (Exception ex)
            {
                throw new CliException("Invalid manifest JSON: " + ex.Message);
            }
            if (manifest == null) throw new CliException("Manifest is empty.");
            manifest.ManifestPath = fullPath;
            manifest.NormalizeAndValidate();
            return manifest;
        }

        private void NormalizeAndValidate()
        {
            if (K2 == null) K2 = new K2ConnectionOptions();
            if (Application == null) Application = new ApplicationOptions();
            if (Verification == null) Verification = new VerificationOptions();
            if (Application.Views == null) Application.Views = new List<ViewDefinition>();
            if (Application.Forms == null) Application.Forms = new List<FormDefinition>();
            if (Application.Lookups == null) Application.Lookups = new List<LookupSourceDefinition>();
            if (Verification.ExpectedViews == null) Verification.ExpectedViews = new List<string>();
            if (Verification.ExpectedForms == null) Verification.ExpectedForms = new List<string>();

            Require(Name, "name");
            Require(K2.Host, "k2.host");
            if (!string.IsNullOrWhiteSpace(Application.CategoryPath))
                throw new CliException("application.categoryPath is no longer supported. Use application.rootCategoryPath; the CLI creates Forms and Views subcategories.");
            Require(Application.RootCategoryPath, "application.rootCategoryPath");
            ValidateRootCategoryPath(Application.RootCategoryPath);
            Require(Application.Theme, "application.theme");
            if (K2.Port <= 0 || K2.Port > 65535) throw new CliException("k2.port must be between 1 and 65535.");
            if (!K2.Integrated)
            {
                Require(K2.UserName, "k2.userName");
                Require(K2.PasswordEnvironmentVariable, "k2.passwordEnvironmentVariable");
            }
            if (Application.Views.Count == 0) throw new CliException("application.views must contain at least one view.");
            if (Application.Forms.Count == 0) throw new CliException("application.forms must contain at least one form.");

            EnsureUnique(Application.Views.Select(x => x == null ? null : x.Name), "view");
            EnsureUnique(Application.Forms.Select(x => x == null ? null : x.Name), "form");
            EnsureUnique(Application.Lookups.Select(x => x == null ? null : x.Name), "lookup");

            foreach (var lookup in Application.Lookups)
            {
                if (lookup == null) throw new CliException("application.lookups cannot contain null entries.");
                Require(lookup.SmartObject, "lookup.smartObject");
                Require(lookup.Method, "lookup.method");
                Require(lookup.ValueProperty, "lookup.valueProperty");
                Require(lookup.DisplayProperty, "lookup.displayProperty");
            }

            var lookupNames = new HashSet<string>(Application.Lookups.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var view in Application.Views)
            {
                if (view == null) throw new CliException("application.views cannot contain null entries.");
                if (view.Properties == null) view.Properties = new List<string>();
                if (view.Methods == null) view.Methods = new List<string>();
                if (view.Options == null) view.Options = new List<string>();
                if (view.LookupControls == null) view.LookupControls = new List<LookupControlDefinition>();
                RequireArtifactName(view.Name, "view.name");
                RejectVersionToken(view.Name, "view.name");
                Require(view.SmartObject, "view.smartObject");
                view.Type = (view.Type ?? string.Empty).Trim().ToLowerInvariant();
                if (!AllowedViewTypes.Contains(view.Type))
                    throw new CliException("Unsupported view type '" + view.Type + "' for " + view.Name + ".");
                ValidateValues(view.Options, AllowedViewOptions, "view option", view.Name);
                EnsureUniqueValues(view.Properties, "property", view.Name);
                EnsureUniqueValues(view.Methods, "method", view.Name);
                if ((view.Type == "list" || view.Type == "content") && string.IsNullOrWhiteSpace(view.DefaultListMethod))
                    view.DefaultListMethod = "List";
                view.Area = NormalizeArea(view.Area, "view", view.Name);
                EnsureUniqueValues(view.LookupControls.Select(x => x == null ? null : x.Property), "lookup control property", view.Name);
                foreach (var control in view.LookupControls)
                {
                    if (control == null) throw new CliException("View '" + view.Name + "' lookupControls cannot contain null entries.");
                    Require(control.Lookup, "view.lookupControls.lookup");
                    if (!lookupNames.Contains(control.Lookup))
                        throw new CliException("View '" + view.Name + "' references undeclared lookup '" + control.Lookup + "'.");
                    if (!view.Properties.Contains(control.Property, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("View '" + view.Name + "' lookup property is not selected in view.properties: " + control.Property);
                    if (view.Type != "capture" && view.Type != "capture-list")
                        throw new CliException("Lookup controls are supported only on capture or capture-list views: " + view.Name);
                }
            }

            var viewNames = new HashSet<string>(Application.Views.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var form in Application.Forms)
            {
                if (form == null) throw new CliException("application.forms cannot contain null entries.");
                if (form.Views == null) form.Views = new List<string>();
                if (form.Options == null) form.Options = new List<string>();
                if (form.Behaviors == null) form.Behaviors = new List<string>();
                RequireArtifactName(form.Name, "form.name");
                RejectVersionToken(form.Name, "form.name");
                if (form.Views.Count == 0) throw new CliException("Form '" + form.Name + "' must reference at least one view.");
                EnsureUniqueValues(form.Views, "view reference", form.Name);
                foreach (var viewName in form.Views)
                    if (!viewNames.Contains(viewName)) throw new CliException("Form '" + form.Name + "' references undeclared view '" + viewName + "'.");
                ValidateValues(form.Options, AllowedFormOptions, "form option", form.Name);
                ValidateValues(form.Behaviors, AllowedFormBehaviors, "form behavior", form.Name);
                form.Area = NormalizeArea(form.Area, "form", form.Name);
            }

            foreach (var lookup in Application.Lookups.Where(x => !string.IsNullOrWhiteSpace(x.AdminForm)))
            {
                var form = Application.Forms.SingleOrDefault(x => string.Equals(x.Name, lookup.AdminForm, StringComparison.OrdinalIgnoreCase));
                if (form == null) throw new CliException("Lookup '" + lookup.Name + "' adminForm is not declared: " + lookup.AdminForm);
                if (!string.Equals(form.Area, "admin", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("Lookup '" + lookup.Name + "' adminForm must use area 'admin': " + lookup.AdminForm);
                var adminViews = Application.Views.Where(x => form.Views.Contains(x.Name, StringComparer.OrdinalIgnoreCase) && string.Equals(x.SmartObject, lookup.SmartObject, StringComparison.OrdinalIgnoreCase)).ToList();
                if (!adminViews.Any(x => (x.Type == "capture" || x.Type == "capture-list") && (x.Options.Contains("all-methods", StringComparer.OrdinalIgnoreCase) || new[] { "Create", "Update", "Delete" }.All(m => x.Methods.Contains(m, StringComparer.OrdinalIgnoreCase)))))
                    throw new CliException("Lookup '" + lookup.Name + "' adminForm must contain a CRUD capture view over " + lookup.SmartObject + ".");
                if (!adminViews.Any(x => x.Type == "capture-list" || ((x.Type == "list" || x.Type == "content") && !string.IsNullOrWhiteSpace(x.DefaultListMethod))))
                    throw new CliException("Lookup '" + lookup.Name + "' adminForm must contain a List view over " + lookup.SmartObject + ".");
            }

            if (Verification.ExpectedViews.Count == 0)
                Verification.ExpectedViews.AddRange(Application.Views.Select(x => x.Name));
            if (Verification.ExpectedForms.Count == 0)
                Verification.ExpectedForms.AddRange(Application.Forms.Select(x => x.Name));

            Uri runtimeBase;
            if (Verification.SmokeTestRuntime &&
                (!Uri.TryCreate(Verification.RuntimeBaseUrl, UriKind.Absolute, out runtimeBase) ||
                 (runtimeBase.Scheme != Uri.UriSchemeHttp && runtimeBase.Scheme != Uri.UriSchemeHttps)))
                throw new CliException("verification.runtimeBaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        private static readonly HashSet<string> AllowedViewTypes = NewSet("capture", "list", "content", "capture-list");
        private static readonly HashSet<string> AllowedViewOptions = NewSet("display-controls", "all-properties", "all-methods", "labels-left", "colon-labels", "toolbar", "editable");
        private static readonly HashSet<string> AllowedFormOptions = NewSet("no-tabs");
        private static readonly HashSet<string> AllowedFormBehaviors = NewSet("load-form-list-click", "refresh-list-form-submit", "refresh-list-form-load");
        private static readonly HashSet<string> AllowedAreas = NewSet("application", "admin");

        private static string NormalizeArea(string value, string kind, string owner)
        {
            var area = string.IsNullOrWhiteSpace(value) ? "application" : value.Trim().ToLowerInvariant();
            if (!AllowedAreas.Contains(area)) throw new CliException("Unsupported " + kind + " area '" + value + "' for " + owner + ".");
            return area;
        }

        private static HashSet<string> NewSet(params string[] values)
        {
            return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }

        private static void ValidateValues(IEnumerable<string> values, HashSet<string> allowed, string kind, string owner)
        {
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value) || !allowed.Contains(value))
                    throw new CliException("Unsupported " + kind + " '" + value + "' for " + owner + ".");
            }
        }

        private static void EnsureUnique(IEnumerable<string> values, string kind)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in values)
            {
                RequireArtifactName(value, kind + ".name");
                if (!seen.Add(value)) throw new CliException("Duplicate " + kind + " name: " + value);
            }
        }

        private static void EnsureUniqueValues(IEnumerable<string> values, string kind, string owner)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) throw new CliException("Empty " + kind + " in " + owner + ".");
                if (!seen.Add(value)) throw new CliException("Duplicate " + kind + " '" + value + "' in " + owner + ".");
            }
        }

        private static void RequireArtifactName(string value, string field)
        {
            Require(value, field);
            if (value.IndexOfAny(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
                throw new CliException("Manifest field '" + field + "' contains unsupported punctuation.");
        }

        private static void ValidateRootCategoryPath(string value)
        {
            var segments = value.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) throw new CliException("application.rootCategoryPath must contain a category name.");
            if (segments.Any(IsVersionSegment))
                throw new CliException("application.rootCategoryPath must not contain version folders. K2 versions artifacts internally.");
            var leaf = segments[segments.Length - 1];
            if (leaf.Equals("Forms", StringComparison.OrdinalIgnoreCase) || leaf.Equals("Views", StringComparison.OrdinalIgnoreCase) || leaf.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                throw new CliException("application.rootCategoryPath must be the application root; the CLI appends Forms, Views, and Admin subcategories.");
        }

        private static bool IsVersionSegment(string value)
        {
            return Regex.IsMatch(value.Trim(), @"^(?:v\d+(?:\.\d+)*|\d+\.\d+(?:\.\d+)*)$", RegexOptions.IgnoreCase);
        }

        private static void RejectVersionToken(string value, string field)
        {
            if (Regex.IsMatch(value, @"(?:^|[\s._-])(?:v\d+(?:\.\d+)*|\d+\.\d+(?:\.\d+)*)(?=$|[\s._-])", RegexOptions.IgnoreCase))
                throw new CliException("Manifest field '" + field + "' must not contain a version token. K2 versions forms and views internally.");
        }

        private static void Require(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new CliException("Manifest field '" + field + "' is required.");
        }
    }

    public sealed class K2ConnectionOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool Integrated { get; set; }
        public string SecurityLabel { get; set; }
        public string Domain { get; set; }
        public string UserName { get; set; }
        public string PasswordEnvironmentVariable { get; set; }

        public K2ConnectionOptions()
        {
            Host = "localhost";
            Port = 5555;
            Integrated = true;
            SecurityLabel = "K2";
        }
    }

    public sealed class ApplicationOptions
    {
        public string RootCategoryPath { get; set; }
        public string CategoryPath { get; set; }
        public string Theme { get; set; }
        public bool ReplaceExisting { get; set; }
        public bool CheckIn { get; set; }
        public List<ViewDefinition> Views { get; set; }
        public List<FormDefinition> Forms { get; set; }
        public List<LookupSourceDefinition> Lookups { get; set; }

        [ScriptIgnore]
        public string ViewsCategoryPath { get { return RootCategoryPath.TrimEnd('\\') + "\\Views"; } }

        [ScriptIgnore]
        public string FormsCategoryPath { get { return RootCategoryPath.TrimEnd('\\') + "\\Forms"; } }

        [ScriptIgnore]
        public string AdminViewsCategoryPath { get { return RootCategoryPath.TrimEnd('\\') + "\\Admin\\Views"; } }

        [ScriptIgnore]
        public string AdminFormsCategoryPath { get { return RootCategoryPath.TrimEnd('\\') + "\\Admin\\Forms"; } }

        public string GetViewCategoryPath(ViewDefinition view) { return view.Area == "admin" ? AdminViewsCategoryPath : ViewsCategoryPath; }
        public string GetFormCategoryPath(FormDefinition form) { return form.Area == "admin" ? AdminFormsCategoryPath : FormsCategoryPath; }

        public ApplicationOptions()
        {
            Theme = "Lithium";
            CheckIn = true;
            Views = new List<ViewDefinition>();
            Forms = new List<FormDefinition>();
            Lookups = new List<LookupSourceDefinition>();
        }
    }

    public sealed class ViewDefinition
    {
        public string Name { get; set; }
        public string SmartObject { get; set; }
        public string Type { get; set; }
        public List<string> Properties { get; set; }
        public List<string> Methods { get; set; }
        public string DefaultListMethod { get; set; }
        public List<string> Options { get; set; }
        public string Area { get; set; }
        public List<LookupControlDefinition> LookupControls { get; set; }

        public ViewDefinition()
        {
            Type = "capture";
            Properties = new List<string>();
            Methods = new List<string>();
            Options = new List<string>();
            Area = "application";
            LookupControls = new List<LookupControlDefinition>();
        }
    }

    public sealed class FormDefinition
    {
        public string Name { get; set; }
        public bool UseLegacyTheme { get; set; }
        public List<string> Views { get; set; }
        public List<string> Options { get; set; }
        public List<string> Behaviors { get; set; }
        public string Area { get; set; }

        public FormDefinition()
        {
            Views = new List<string>();
            Options = new List<string>();
            Behaviors = new List<string>();
            Area = "application";
        }
    }

    public sealed class LookupSourceDefinition
    {
        public string Name { get; set; }
        public string SmartObject { get; set; }
        public string Method { get; set; }
        public string ValueProperty { get; set; }
        public string DisplayProperty { get; set; }
        public string AdminForm { get; set; }

        public LookupSourceDefinition() { Method = "List"; }
    }

    public sealed class LookupControlDefinition
    {
        public string Property { get; set; }
        public string Lookup { get; set; }
        public bool AllowEmptySelection { get; set; }
    }

    public sealed class VerificationOptions
    {
        public List<string> ExpectedViews { get; set; }
        public List<string> ExpectedForms { get; set; }
        public bool SmokeTestRuntime { get; set; }
        public string RuntimeBaseUrl { get; set; }

        public VerificationOptions()
        {
            ExpectedViews = new List<string>();
            ExpectedForms = new List<string>();
            SmokeTestRuntime = true;
            RuntimeBaseUrl = "http://localhost/Runtime";
        }
    }

    internal sealed class CliException : Exception
    {
        public CliException(string message) : base(message) { }
    }
}
