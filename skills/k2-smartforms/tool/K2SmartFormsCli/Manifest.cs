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
            if (Application.CommonHeader != null)
            {
                if (Application.CommonHeader.Parameters == null) Application.CommonHeader.Parameters = new Dictionary<string, string>();
                if (Application.CommonHeader.ServerLoadControlTransfers == null) Application.CommonHeader.ServerLoadControlTransfers = new Dictionary<string, string>();
                if (Application.CommonHeader.ServerRules == null) Application.CommonHeader.ServerRules = new List<string>();
                if (!Application.CommonHeader.Enabled && string.IsNullOrWhiteSpace(Application.CommonHeader.Reason))
                    throw new CliException("application.commonHeader.reason is required when the environment common header is disabled.");
                if (Application.CommonHeader.Enabled && !string.IsNullOrWhiteSpace(Application.CommonHeader.View))
                {
                    if (Application.CommonHeader.Parameters.Keys.Any(string.IsNullOrWhiteSpace))
                        throw new CliException("application.commonHeader.parameters contains an empty parameter name.");
                    if (Application.CommonHeader.ServerLoadControlTransfers.Keys.Any(string.IsNullOrWhiteSpace))
                        throw new CliException("application.commonHeader.serverLoadControlTransfers contains an empty control name.");
                    EnsureUniqueValues(Application.CommonHeader.ServerRules, "common header server rule", "application.commonHeader");
                }
            }
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
                if (view.ReadOnlyProperties == null) view.ReadOnlyProperties = new List<string>();
                if (view.HiddenProperties == null) view.HiddenProperties = new List<string>();
                if (view.PropertyLabels == null) view.PropertyLabels = new Dictionary<string, string>();
                if (view.DefaultValues == null) view.DefaultValues = new Dictionary<string, string>();
                if (view.SingleLineProperties == null) view.SingleLineProperties = new List<string>();
                if (view.RequiredProperties == null) view.RequiredProperties = new List<string>();
                if (view.LookupRequiredProperties == null) view.LookupRequiredProperties = new List<string>();
                if (view.Sections == null) view.Sections = new List<ViewSectionDefinition>();
                if (view.Help == null) view.Help = new List<ViewHelpDefinition>();
                if (view.HiddenVariables == null) view.HiddenVariables = new List<HiddenVariableDefinition>();
                if (view.Charts == null) view.Charts = new List<ViewChartDefinition>();
                if (view.MetricCards == null) view.MetricCards = new List<ViewMetricCardDefinition>();
                if (view.LifecycleTrackers == null) view.LifecycleTrackers = new List<ViewLifecycleDefinition>();
                RequireArtifactName(view.Name, "view.name");
                RejectVersionToken(view.Name, "view.name");
                Require(view.SmartObject, "view.smartObject");
                view.Type = (view.Type ?? string.Empty).Trim().ToLowerInvariant();
                if (!AllowedViewTypes.Contains(view.Type))
                    throw new CliException("Unsupported view type '" + view.Type + "' for " + view.Name + ".");
                if (!view.LayoutColumns.HasValue)
                    view.LayoutColumns = view.Type == "capture" ? 4 : 2;
                ValidateValues(view.Options, AllowedViewOptions, "view option", view.Name);
                EnsureUniqueValues(view.Properties, "property", view.Name);
                EnsureUniqueValues(view.Methods, "method", view.Name);
                EnsureUniqueValues(view.ReadOnlyProperties, "read-only property", view.Name);
                EnsureUniqueValues(view.HiddenProperties, "hidden property", view.Name);
                EnsureUniqueValues(view.SingleLineProperties, "single-line property", view.Name);
                EnsureUniqueValues(view.RequiredProperties, "required property", view.Name);
                EnsureUniqueValues(view.LookupRequiredProperties, "lookup-required property", view.Name);
                EnsureUniqueValues(view.HiddenVariables.Select(x => x == null ? null : x.Name), "hidden variable", view.Name);
                foreach (var variable in view.HiddenVariables)
                {
                    if (variable == null) throw new CliException("View '" + view.Name + "' hiddenVariables cannot contain null entries.");
                    Require(variable.Name, "view.hiddenVariables.name");
                    Require(variable.DataType, "view.hiddenVariables.dataType");
                }
                if (view.HiddenVariables.Count > 0 && view.Type != "capture")
                    throw new CliException("View '" + view.Name + "' hiddenVariables are currently supported only on capture/item views.");
                EnsureUniqueValues(view.Charts.Select(x => x == null ? null : x.Name), "chart", view.Name);
                foreach (var chart in view.Charts)
                {
                    if (chart == null) throw new CliException("View '" + view.Name + "' charts cannot contain null entries.");
                    Require(chart.Name, "view.charts.name"); Require(chart.Title, "view.charts.title");
                    Require(chart.CategoryProperty, "view.charts.categoryProperty"); Require(chart.ValueProperty, "view.charts.valueProperty");
                    chart.Type = (chart.Type ?? "column").Trim().ToLowerInvariant();
                    if (!AllowedChartTypes.Contains(chart.Type)) throw new CliException("Unsupported chart type '" + chart.Type + "' for " + view.Name + ".");
                    if (view.Type != "capture") throw new CliException("View '" + view.Name + "' charts require a capture View; pair it with a list View for the accessible data-table alternative.");
                    if (!view.Properties.Contains(chart.CategoryProperty, StringComparer.OrdinalIgnoreCase) || !view.Properties.Contains(chart.ValueProperty, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("View '" + view.Name + "' chart properties must be selected in view.properties: " + chart.CategoryProperty + ", " + chart.ValueProperty);
                    if (chart.Height < 180 || chart.Height > 800) throw new CliException("View '" + view.Name + "' chart height must be between 180 and 800 pixels.");
                }
                EnsureUniqueValues(view.MetricCards.Select(x => x == null ? null : x.Property), "metric card property", view.Name);
                foreach (var card in view.MetricCards)
                {
                    if (card == null) throw new CliException("View '" + view.Name + "' metricCards cannot contain null entries.");
                    Require(card.Property, "view.metricCards.property"); Require(card.Label, "view.metricCards.label");
                    if (view.Type != "capture") throw new CliException("View '" + view.Name + "' metricCards require a capture View so their card layout is rendered natively.");
                    if (!view.Properties.Contains(card.Property, StringComparer.OrdinalIgnoreCase)) throw new CliException("View '" + view.Name + "' metric card property must be selected in view.properties: " + card.Property);
                    if (!AllowedMetricTones.Contains((card.Tone ?? "neutral").Trim().ToLowerInvariant())) throw new CliException("Unsupported metric card tone '" + card.Tone + "' for " + view.Name + ".");
                }
                if (view.MetricCards.Count > 6) throw new CliException("View '" + view.Name + "' supports at most six metric cards.");
                EnsureUniqueValues(view.LifecycleTrackers.Select(x => x == null ? null : x.Property), "lifecycle tracker property", view.Name);
                foreach (var tracker in view.LifecycleTrackers)
                {
                    if (tracker == null) throw new CliException("View '" + view.Name + "' lifecycleTrackers cannot contain null entries.");
                    Require(tracker.Property, "view.lifecycleTrackers.property"); Require(tracker.Name, "view.lifecycleTrackers.name");
                    if (view.Type != "capture") throw new CliException("View '" + view.Name + "' lifecycleTrackers currently require type capture.");
                    if (!view.Properties.Contains(tracker.Property, StringComparer.OrdinalIgnoreCase)) throw new CliException("View '" + view.Name + "' lifecycle tracker property must be selected in view.properties: " + tracker.Property);
                    if (tracker.Stages == null || tracker.Stages.Count < 2) throw new CliException("View '" + view.Name + "' lifecycle tracker requires at least two stages.");
                    EnsureUniqueValues(tracker.Stages.Select(x => x == null ? null : x.Code), "lifecycle stage code", view.Name);
                    foreach (var stage in tracker.Stages) { if (stage == null) throw new CliException("View '" + view.Name + "' lifecycle tracker stages cannot contain null entries."); Require(stage.Code, "view.lifecycleTrackers.stages.code"); Require(stage.Label, "view.lifecycleTrackers.stages.label"); }
                }
                if (view.LayoutColumns != 2 && view.LayoutColumns != 4)
                    throw new CliException("View '" + view.Name + "' layoutColumns must be 2 or 4.");
                if (view.LayoutColumns == 4 && view.Type != "capture")
                    throw new CliException("View '" + view.Name + "' can use layoutColumns=4 only for capture/item views.");
                if (!view.Responsive && view.Type == "capture")
                    throw new CliException("View '" + view.Name + "' disables responsive table behavior. Capture Views must remain responsive; use layoutColumns=2 for narrow layouts.");
                foreach (var property in view.ReadOnlyProperties)
                    if (!view.Properties.Contains(property, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("View '" + view.Name + "' readOnlyProperties entry is not selected in view.properties: " + property);
                foreach (var property in view.HiddenProperties)
                    if (!view.Properties.Contains(property, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("View '" + view.Name + "' hiddenProperties entry is not selected in view.properties: " + property);
                foreach (var property in view.SingleLineProperties.Concat(view.RequiredProperties).Concat(view.LookupRequiredProperties))
                    if (!view.Properties.Contains(property, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("View '" + view.Name + "' field contract references a property not selected in view.properties: " + property);
                foreach (var property in view.RequiredProperties)
                    if (view.HiddenProperties.Contains(property, StringComparer.OrdinalIgnoreCase) ||
                        view.ReadOnlyProperties.Contains(property, StringComparer.OrdinalIgnoreCase) ||
                        view.DefaultValues.Keys.Contains(property, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("View '" + view.Name + "' requiredProperties must be visible, editable, user-supplied fields: " + property);
                EnsureUniqueValues(view.Sections.Select(x => x == null ? null : x.Title), "section title", view.Name);
                if (view.Sections.Count > 0)
                {
                    if (view.Type != "capture") throw new CliException("View '" + view.Name + "' sections are supported only on capture/item Views.");
                    var sectionProperties = new List<string>();
                    foreach (var section in view.Sections)
                    {
                        if (section == null) throw new CliException("View '" + view.Name + "' sections cannot contain null entries.");
                        Require(section.Title, "view.sections.title");
                        if (section.Properties == null || section.Properties.Count == 0) throw new CliException("View '" + view.Name + "' section '" + section.Title + "' must contain properties.");
                        sectionProperties.AddRange(section.Properties);
                    }
                    EnsureUniqueValues(sectionProperties, "section property", view.Name);
                    var visible = view.Properties.Where(x => !view.HiddenProperties.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();
                    if (!sectionProperties.SequenceEqual(visible, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("View '" + view.Name + "' sections must contain every visible property exactly once and in view.properties order.");
                }
                EnsureUniqueValues(view.Help.Select(x => x == null ? null : x.Property), "help property", view.Name);
                foreach (var help in view.Help)
                {
                    if (help == null) throw new CliException("View '" + view.Name + "' help cannot contain null entries.");
                    if (view.Type != "capture") throw new CliException("View '" + view.Name + "' help is supported only on capture/item Views.");
                    Require(help.Property, "view.help.property");
                    Require(help.Body, "view.help.body");
                    if (!view.Properties.Contains(help.Property, StringComparer.OrdinalIgnoreCase) ||
                        view.HiddenProperties.Contains(help.Property, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("View '" + view.Name + "' help property must be selected and visible: " + help.Property);
                    if (string.IsNullOrWhiteSpace(help.LinkText)) help.LinkText = "More info";
                    if (string.IsNullOrWhiteSpace(help.Title)) help.Title = view.PropertyLabels.ContainsKey(help.Property) ? view.PropertyLabels[help.Property] : help.Property;
                }
                if (view.SingleLineProperties.Count > 0 && view.Type != "capture")
                    throw new CliException("View '" + view.Name + "' singleLineProperties are supported only on capture/item Views.");
                foreach (var label in view.PropertyLabels)
                {
                    if (!view.Properties.Contains(label.Key, StringComparer.OrdinalIgnoreCase)) throw new CliException("View '" + view.Name + "' propertyLabels entry is not selected in view.properties: " + label.Key);
                    if (string.IsNullOrWhiteSpace(label.Value)) throw new CliException("View '" + view.Name + "' propertyLabels value is empty for property '" + label.Key + "'.");
                }
                foreach (var value in view.DefaultValues)
                {
                    if (string.IsNullOrWhiteSpace(value.Key))
                        throw new CliException("View '" + view.Name + "' defaultValues contains an empty property name.");
                    if (!view.Properties.Contains(value.Key, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("View '" + view.Name + "' defaultValues entry is not selected in view.properties: " + value.Key);
                    if (value.Value == null)
                        throw new CliException("View '" + view.Name + "' defaultValues must use an explicit string value for property '" + value.Key + "'.");
                }
                if (view.DefaultValues.Count > 0 && view.Type != "capture" && view.Type != "capture-list")
                    throw new CliException("View '" + view.Name + "' defaultValues are supported only on capture or capture-list views.");
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
                    if (control.Cascade != null)
                    {
                        Require(control.Cascade.ParentProperty, "view.lookupControls.cascade.parentProperty");
                        Require(control.Cascade.ParentJoinProperty, "view.lookupControls.cascade.parentJoinProperty");
                        Require(control.Cascade.ChildJoinProperty, "view.lookupControls.cascade.childJoinProperty");
                        if (string.Equals(control.Property, control.Cascade.ParentProperty, StringComparison.OrdinalIgnoreCase))
                            throw new CliException("View '" + view.Name + "' cascading lookup cannot use itself as its parent: " + control.Property);
                        if (!view.Properties.Contains(control.Cascade.ParentProperty, StringComparer.OrdinalIgnoreCase))
                            throw new CliException("View '" + view.Name + "' cascading lookup parent is not selected in view.properties: " + control.Cascade.ParentProperty);
                    }
                }
                var lookupProperties = new HashSet<string>(view.LookupControls.Select(x => x.Property), StringComparer.OrdinalIgnoreCase);
                var missingLookups = view.LookupRequiredProperties.Where(x => !lookupProperties.Contains(x)).ToList();
                if (missingLookups.Count > 0)
                    throw new CliException("View '" + view.Name + "' requires declared lookupControls for controlled properties: " + string.Join(", ", missingLookups.ToArray()) + ".");
                var defaultedLookups = view.DefaultValues.Keys.Where(lookupProperties.Contains).ToList();
                if (defaultedLookups.Count > 0)
                    throw new CliException("View '" + view.Name + "' defaultValues cannot initialize lookup controls: " + string.Join(", ", defaultedLookups.ToArray()) + ". Keep the lookup editable; declarative default selection is not supported.");
            }

            var viewNames = new HashSet<string>(Application.Views.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var form in Application.Forms)
            {
                if (form == null) throw new CliException("application.forms cannot contain null entries.");
                if (form.Views == null) form.Views = new List<string>();
                if (form.Options == null) form.Options = new List<string>();
                if (form.Behaviors == null) form.Behaviors = new List<string>();
                if (form.Tabs == null) form.Tabs = new List<FormTabDefinition>();
                if (form.ListClickTabNavigation == null) form.ListClickTabNavigation = new List<ListClickTabNavigationDefinition>();
                if (form.MasterDetail != null && form.MasterDetail.Details == null) form.MasterDetail.Details = new List<MasterDetailChildDefinition>();
                if (form.ViewTitles == null) form.ViewTitles = new Dictionary<string, string>();
                if (form.UntitledViews == null) form.UntitledViews = new Dictionary<string, string>();
                RequireArtifactName(form.Name, "form.name");
                RejectVersionToken(form.Name, "form.name");
                if (form.Views.Count == 0) throw new CliException("Form '" + form.Name + "' must reference at least one view.");
                EnsureUniqueValues(form.Views, "view reference", form.Name);
                foreach (var viewName in form.Views)
                    if (!viewNames.Contains(viewName)) throw new CliException("Form '" + form.Name + "' references undeclared view '" + viewName + "'.");
                foreach (var item in form.ViewTitles)
                {
                    if (!form.Views.Contains(item.Key, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("Form '" + form.Name + "' declares a title for a view not present on the form: " + item.Key);
                    if (string.IsNullOrWhiteSpace(item.Value))
                        throw new CliException("Form '" + form.Name + "' viewTitles values must be non-empty. Use untitledViews with a reason to suppress a title: " + item.Key);
                }
                foreach (var item in form.UntitledViews)
                {
                    if (!form.Views.Contains(item.Key, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("Form '" + form.Name + "' declares an untitled exception for a view not present on the form: " + item.Key);
                    if (string.IsNullOrWhiteSpace(item.Value))
                        throw new CliException("Form '" + form.Name + "' untitledViews must explain why the title is suppressed: " + item.Key);
                    if (form.ViewTitles.Keys.Contains(item.Key, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("Form '" + form.Name + "' cannot both title and suppress the same view: " + item.Key);
                }
                ValidateValues(form.Options, AllowedFormOptions, "form option", form.Name);
                ValidateValues(form.Behaviors, AllowedFormBehaviors, "form behavior", form.Name);
                form.Area = NormalizeArea(form.Area, "form", form.Name);
                ValidateTabs(form);
                ValidateWorkflowStartButton(form);
                ValidateListClickTabNavigation(form, Application.Views);
                ValidateMasterDetail(form, Application.Views);
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
        private static readonly HashSet<string> AllowedWorklistActions = NewSet("viewWorkflow", "sleep", "redirect", "release", "share");
        private static readonly HashSet<string> AllowedChartTypes = NewSet("column", "bar", "line", "area", "pie", "donut");
        private static readonly HashSet<string> AllowedMetricTones = NewSet("neutral", "positive", "warning", "critical");

        private static void ValidateMasterDetail(FormDefinition form, IEnumerable<ViewDefinition> views)
        {
            var relationship = form.MasterDetail;
            if (relationship == null) return;
            Require(relationship.MasterView, "form.masterDetail.masterView");
            Require(relationship.MasterKeyProperty, "form.masterDetail.masterKeyProperty");
            Require(relationship.MasterCreateMethod, "form.masterDetail.masterCreateMethod");
            Require(relationship.MasterUpdateMethod, "form.masterDetail.masterUpdateMethod");
            Require(relationship.MasterReadMethod, "form.masterDetail.masterReadMethod");
            Require(relationship.SaveButtonText, "form.masterDetail.saveButtonText");
            Require(relationship.SuccessMessageTitle, "form.masterDetail.successMessageTitle");
            Require(relationship.SuccessMessageBody, "form.masterDetail.successMessageBody");
            if (!form.Views.Contains(relationship.MasterView, StringComparer.OrdinalIgnoreCase))
                throw new CliException("Form '" + form.Name + "' masterDetail.masterView is not present on the form: " + relationship.MasterView);
            var master = views.Single(x => string.Equals(x.Name, relationship.MasterView, StringComparison.OrdinalIgnoreCase));
            if (master.Type != "capture") throw new CliException("Form '" + form.Name + "' master-detail master must be a capture/item view: " + master.Name);
            if (!master.Properties.Contains(relationship.MasterKeyProperty, StringComparer.OrdinalIgnoreCase))
                throw new CliException("Form '" + form.Name + "' master view must select the generated key property so it can be returned by Create: " + relationship.MasterKeyProperty);
            foreach (var method in new[] { relationship.MasterCreateMethod, relationship.MasterUpdateMethod, relationship.MasterReadMethod })
                if (!master.Methods.Contains(method, StringComparer.OrdinalIgnoreCase) && !master.Options.Contains("all-methods", StringComparer.OrdinalIgnoreCase))
                    throw new CliException("Form '" + form.Name + "' master view does not select required master-detail method '" + method + "'.");
            if (relationship.Details.Count == 0) throw new CliException("Form '" + form.Name + "' masterDetail.details must contain at least one child view.");
            EnsureUniqueValues(relationship.Details.Select(x => x == null ? null : x.View), "master-detail child view", form.Name);
            foreach (var child in relationship.Details)
            {
                if (child == null) throw new CliException("Form '" + form.Name + "' masterDetail.details cannot contain null entries.");
                Require(child.ForeignKeyProperty, "form.masterDetail.details.foreignKeyProperty");
                Require(child.CreateMethod, "form.masterDetail.details.createMethod");
                Require(child.UpdateMethod, "form.masterDetail.details.updateMethod");
                Require(child.DeleteMethod, "form.masterDetail.details.deleteMethod");
                Require(child.ListMethod, "form.masterDetail.details.listMethod");
                if (!form.Views.Contains(child.View, StringComparer.OrdinalIgnoreCase))
                    throw new CliException("Form '" + form.Name + "' master-detail child view is not present on the form: " + child.View);
                var detail = views.Single(x => string.Equals(x.Name, child.View, StringComparison.OrdinalIgnoreCase));
                if (detail.Type != "capture-list" || !detail.Options.Contains("editable", StringComparer.OrdinalIgnoreCase))
                    throw new CliException("Form '" + form.Name + "' master-detail child must be an editable capture-list view: " + detail.Name);
                foreach (var method in new[] { child.CreateMethod, child.UpdateMethod, child.DeleteMethod, child.ListMethod })
                    if (!detail.Methods.Contains(method, StringComparer.OrdinalIgnoreCase) && !detail.Options.Contains("all-methods", StringComparer.OrdinalIgnoreCase) &&
                        !string.Equals(detail.DefaultListMethod, method, StringComparison.OrdinalIgnoreCase))
                        throw new CliException("Form '" + form.Name + "' detail view '" + detail.Name + "' does not select required method '" + method + "'.");
            }
            if (relationship.Review != null)
            {
                Require(relationship.Review.View, "form.masterDetail.review.view");
                Require(relationship.Review.KeyProperty, "form.masterDetail.review.keyProperty");
                Require(relationship.Review.ReadMethod, "form.masterDetail.review.readMethod");
                Require(relationship.Review.Tab, "form.masterDetail.review.tab");
                if (!form.Views.Contains(relationship.Review.View, StringComparer.OrdinalIgnoreCase)) throw new CliException("Form '" + form.Name + "' master-detail review View is not present on the form: " + relationship.Review.View);
                var review = views.Single(x => string.Equals(x.Name, relationship.Review.View, StringComparison.OrdinalIgnoreCase));
                if (review.Type != "capture") throw new CliException("Form '" + form.Name + "' review must be a capture/item View: " + review.Name);
                if (!review.Properties.Contains(relationship.Review.KeyProperty, StringComparer.OrdinalIgnoreCase)) throw new CliException("Form '" + form.Name + "' review View must select its key property: " + relationship.Review.KeyProperty);
                if (!review.Methods.Contains(relationship.Review.ReadMethod, StringComparer.OrdinalIgnoreCase) && !review.Options.Contains("all-methods", StringComparer.OrdinalIgnoreCase)) throw new CliException("Form '" + form.Name + "' review View does not select Read method '" + relationship.Review.ReadMethod + "'.");
                var reviewTab = form.Tabs.FirstOrDefault(x => string.Equals(x.Name, relationship.Review.Tab, StringComparison.OrdinalIgnoreCase));
                if (reviewTab == null || !reviewTab.Views.Contains(relationship.Review.View, StringComparer.OrdinalIgnoreCase)) throw new CliException("Form '" + form.Name + "' review View must be placed on review tab '" + relationship.Review.Tab + "'.");
            }
        }

        private static void ValidateWorkflowStartButton(FormDefinition form)
        {
            var button = form.WorkflowStartButton;
            if (button == null) return;
            RequireArtifactName(button.Name, "form.workflowStartButton.name");
            Require(button.Text, "form.workflowStartButton.text");
            RequireArtifactName(button.Tab, "form.workflowStartButton.tab");
            if (form.Tabs.Count == 0)
                throw new CliException("Form '" + form.Name + "' workflowStartButton requires a tabbed form.");
            if (!form.Tabs.Any(x => string.Equals(x.Name, button.Tab, StringComparison.OrdinalIgnoreCase)))
                throw new CliException("Form '" + form.Name + "' workflowStartButton references undeclared tab '" + button.Tab + "'.");
        }

        private static void ValidateTabs(FormDefinition form)
        {
            if (form.Tabs.Count == 0) return;
            if (form.Options.Contains("no-tabs", StringComparer.OrdinalIgnoreCase))
                throw new CliException("Form '" + form.Name + "' cannot combine tabs with the no-tabs option.");
            if (form.Tabs.Count < 2)
                throw new CliException("Form '" + form.Name + "' must declare at least two tabs when form.tabs is used.");

            EnsureUniqueValues(form.Tabs.Select(x => x == null ? null : x.Name), "tab name", form.Name);
            var placedViews = new List<string>();
            var worklistCount = 0;
            foreach (var tab in form.Tabs)
            {
                if (tab == null) throw new CliException("Form '" + form.Name + "' tabs cannot contain null entries.");
                if (tab.Views == null) tab.Views = new List<string>();
                RequireArtifactName(tab.Name, "form.tabs.name");
                RejectVersionToken(tab.Name, "form.tabs.name");
                EnsureUniqueValues(tab.Views, "tab view reference", form.Name + " / " + tab.Name);
                var hasViews = tab.Views.Count > 0;
                var hasWorklist = tab.Worklist != null;
                if (hasViews == hasWorklist)
                    throw new CliException("Form '" + form.Name + "' tab '" + tab.Name + "' must contain either views or one worklist, but not both.");
                foreach (var viewName in tab.Views)
                {
                    if (!form.Views.Contains(viewName, StringComparer.OrdinalIgnoreCase))
                        throw new CliException("Form '" + form.Name + "' tab '" + tab.Name + "' references a view not declared in form.views: " + viewName);
                    placedViews.Add(viewName);
                }
                if (hasWorklist)
                {
                    worklistCount++;
                    var worklist = tab.Worklist;
                    if (worklist.Rows < 1 || worklist.Rows > 200)
                        throw new CliException("Form '" + form.Name + "' worklist rows must be between 1 and 200.");
                    if (worklist.RefreshIntervalSeconds < 0)
                        throw new CliException("Form '" + form.Name + "' worklist refreshIntervalSeconds cannot be negative.");
                    Require(worklist.Height, "form.tabs.worklist.height");
                    if (worklist.Actions == null) worklist.Actions = new List<string>();
                    EnsureUniqueValues(worklist.Actions, "worklist action", form.Name + " / " + tab.Name);
                    ValidateValues(worklist.Actions, AllowedWorklistActions, "worklist action", form.Name + " / " + tab.Name);
                }
            }
            EnsureUniqueValues(placedViews, "tabbed view placement", form.Name);
            var omitted = form.Views.Where(x => !placedViews.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (omitted.Length > 0)
                throw new CliException("Form '" + form.Name + "' tabs do not place declared view(s): " + string.Join(", ", omitted));
            if (worklistCount > 1)
                throw new CliException("Form '" + form.Name + "' supports at most one Worklist tab.");
        }

        private static void ValidateListClickTabNavigation(FormDefinition form, IEnumerable<ViewDefinition> views)
        {
            if (form.ListClickTabNavigation.Count == 0) return;
            if (form.Tabs.Count == 0)
                throw new CliException("Form '" + form.Name + "' listClickTabNavigation requires form.tabs.");
            if (!form.Behaviors.Contains("load-form-list-click", StringComparer.OrdinalIgnoreCase))
                throw new CliException("Form '" + form.Name + "' listClickTabNavigation requires behavior 'load-form-list-click' so the selected item is loaded before the tab changes.");
            EnsureUniqueValues(form.ListClickTabNavigation.Select(x => x == null ? null : x.SourceView), "list-click tab source view", form.Name);
            foreach (var navigation in form.ListClickTabNavigation)
            {
                if (navigation == null) throw new CliException("Form '" + form.Name + "' listClickTabNavigation cannot contain null entries.");
                Require(navigation.SourceView, "form.listClickTabNavigation.sourceView");
                Require(navigation.TargetTab, "form.listClickTabNavigation.targetTab");
                if (!form.Views.Contains(navigation.SourceView, StringComparer.OrdinalIgnoreCase))
                    throw new CliException("Form '" + form.Name + "' listClickTabNavigation references a source view not present on the form: " + navigation.SourceView);
                var sourceView = views.Single(x => string.Equals(x.Name, navigation.SourceView, StringComparison.OrdinalIgnoreCase));
                if (!string.Equals(sourceView.Type, "list", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("Form '" + form.Name + "' listClickTabNavigation source must be a list view: " + navigation.SourceView);
                var sourceTab = form.Tabs.Single(x => x.Views.Contains(navigation.SourceView, StringComparer.OrdinalIgnoreCase));
                var targetTab = form.Tabs.SingleOrDefault(x => string.Equals(x.Name, navigation.TargetTab, StringComparison.OrdinalIgnoreCase));
                if (targetTab == null)
                    throw new CliException("Form '" + form.Name + "' listClickTabNavigation references an unknown target tab: " + navigation.TargetTab);
                if (object.ReferenceEquals(sourceTab, targetTab))
                    throw new CliException("Form '" + form.Name + "' listClickTabNavigation target must differ from the source view's tab: " + navigation.TargetTab);
            }
        }

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
        public string StyleProfile { get; set; }
        public string SolutionCode { get; set; }
        public CommonHeaderDefinition CommonHeader { get; set; }
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

    public sealed class CommonHeaderDefinition
    {
        public bool Enabled { get; set; }
        public string Environment { get; set; }
        public string View { get; set; }
        public Guid ViewGuid { get; set; }
        public string Title { get; set; }
        public string InstanceName { get; set; }
        public bool? IsCollapsible { get; set; }
        public string InitializeEvent { get; set; }
        public List<string> ServerRules { get; set; }
        public bool ServerRulesBeforeControlTransfers { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public Dictionary<string, string> ServerLoadControlTransfers { get; set; }
        public CommonFooterDefinition Footer { get; set; }
        public string Reason { get; set; }

        public CommonHeaderDefinition()
        {
            Enabled = true;
            ServerRules = new List<string>();
            Parameters = new Dictionary<string, string>();
            ServerLoadControlTransfers = new Dictionary<string, string>();
        }
    }

    public sealed class CommonFooterDefinition
    {
        public string View { get; set; }
        public Guid ViewGuid { get; set; }
        public string Title { get; set; }
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
        public List<string> ReadOnlyProperties { get; set; }
        public List<string> HiddenProperties { get; set; }
        public Dictionary<string, string> PropertyLabels { get; set; }
        public Dictionary<string, string> DefaultValues { get; set; }
        public int? LayoutColumns { get; set; }
        public bool Responsive { get; set; }
        public List<string> SingleLineProperties { get; set; }
        public List<string> RequiredProperties { get; set; }
        public List<string> LookupRequiredProperties { get; set; }
        public List<ViewSectionDefinition> Sections { get; set; }
        public List<ViewHelpDefinition> Help { get; set; }
        public List<HiddenVariableDefinition> HiddenVariables { get; set; }
        public List<ViewChartDefinition> Charts { get; set; }
        public List<ViewMetricCardDefinition> MetricCards { get; set; }
        public List<ViewLifecycleDefinition> LifecycleTrackers { get; set; }

        public ViewDefinition()
        {
            Type = "capture";
            Properties = new List<string>();
            Methods = new List<string>();
            Options = new List<string>();
            Area = "application";
            LookupControls = new List<LookupControlDefinition>();
            ReadOnlyProperties = new List<string>();
            HiddenProperties = new List<string>();
            PropertyLabels = new Dictionary<string, string>();
            DefaultValues = new Dictionary<string, string>();
            LayoutColumns = null;
            Responsive = true;
            SingleLineProperties = new List<string>();
            RequiredProperties = new List<string>();
            LookupRequiredProperties = new List<string>();
            Sections = new List<ViewSectionDefinition>();
            Help = new List<ViewHelpDefinition>();
            HiddenVariables = new List<HiddenVariableDefinition>();
            Charts = new List<ViewChartDefinition>();
            MetricCards = new List<ViewMetricCardDefinition>();
            LifecycleTrackers = new List<ViewLifecycleDefinition>();
        }
    }

    public sealed class ViewSectionDefinition
    {
        public string Title { get; set; }
        public List<string> Properties { get; set; }
        public ViewSectionDefinition() { Properties = new List<string>(); }
    }

    public sealed class ViewHelpDefinition
    {
        public string Property { get; set; }
        public string LinkText { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public ViewHelpDefinition() { LinkText = "More info"; }
    }

    public sealed class ViewChartDefinition
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public string CategoryProperty { get; set; }
        public string ValueProperty { get; set; }
        public int Height { get; set; }
        public bool ShowLegend { get; set; }
        public bool ShowLabels { get; set; }
        public string EmptyState { get; set; }
        public ViewChartDefinition() { Type = "column"; Height = 275; EmptyState = "No data to display"; }
    }

    public sealed class ViewMetricCardDefinition
    {
        public string Property { get; set; }
        public string Label { get; set; }
        public string Tone { get; set; }
        public string Explanation { get; set; }
        public ViewMetricCardDefinition() { Tone = "neutral"; }
    }

    public sealed class ViewLifecycleDefinition
    {
        public string Name { get; set; }
        public string Property { get; set; }
        public List<ViewLifecycleStageDefinition> Stages { get; set; }
        public ViewLifecycleDefinition() { Name = "Lifecycle"; Stages = new List<ViewLifecycleStageDefinition>(); }
    }

    public sealed class ViewLifecycleStageDefinition
    {
        public string Code { get; set; }
        public string Label { get; set; }
    }

    public sealed class HiddenVariableDefinition
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string DefaultValue { get; set; }
        public HiddenVariableDefinition() { DataType = "Text"; }
    }

    public sealed class FormDefinition
    {
        public string Name { get; set; }
        public bool UseLegacyTheme { get; set; }
        public List<string> Views { get; set; }
        public List<string> Options { get; set; }
        public List<string> Behaviors { get; set; }
        public string Area { get; set; }
        public List<FormTabDefinition> Tabs { get; set; }
        public List<ListClickTabNavigationDefinition> ListClickTabNavigation { get; set; }
        public MasterDetailFormDefinition MasterDetail { get; set; }
        public WorkflowStartButtonDefinition WorkflowStartButton { get; set; }
        public Dictionary<string, string> ViewTitles { get; set; }
        public Dictionary<string, string> UntitledViews { get; set; }

        public FormDefinition()
        {
            Views = new List<string>();
            Options = new List<string>();
            Behaviors = new List<string>();
            Area = "application";
            Tabs = new List<FormTabDefinition>();
            ListClickTabNavigation = new List<ListClickTabNavigationDefinition>();
            ViewTitles = new Dictionary<string, string>();
            UntitledViews = new Dictionary<string, string>();
        }

        public string ResolveViewTitle(string viewName)
        {
            if (UntitledViews.Keys.Contains(viewName, StringComparer.OrdinalIgnoreCase)) return string.Empty;
            var custom = ViewTitles.FirstOrDefault(x => string.Equals(x.Key, viewName, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(custom.Key) ? viewName : custom.Value;
        }
    }

    public sealed class WorkflowStartButtonDefinition
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public string Tab { get; set; }
    }

    public sealed class MasterDetailFormDefinition
    {
        public string MasterView { get; set; }
        public string MasterKeyProperty { get; set; }
        public string MasterCreateMethod { get; set; }
        public string MasterUpdateMethod { get; set; }
        public string MasterReadMethod { get; set; }
        public string SaveButtonText { get; set; }
        public string SuccessMessageTitle { get; set; }
        public string SuccessMessageBody { get; set; }
        public List<MasterDetailChildDefinition> Details { get; set; }
        public MasterDetailReviewDefinition Review { get; set; }

        public MasterDetailFormDefinition()
        {
            MasterCreateMethod = "Create";
            MasterUpdateMethod = "Update";
            MasterReadMethod = "Read";
            SaveButtonText = "Save";
            SuccessMessageTitle = "Saved";
            SuccessMessageBody = "The record and its line items were saved successfully.";
            Details = new List<MasterDetailChildDefinition>();
        }
    }

    public sealed class MasterDetailReviewDefinition
    {
        public string View { get; set; }
        public string KeyProperty { get; set; }
        public string ReadMethod { get; set; }
        public string Tab { get; set; }
        public bool HiddenUntilSaved { get; set; }
        public MasterDetailReviewDefinition() { ReadMethod = "Read"; HiddenUntilSaved = true; }
    }

    public sealed class MasterDetailChildDefinition
    {
        public string View { get; set; }
        public string ForeignKeyProperty { get; set; }
        public string CreateMethod { get; set; }
        public string UpdateMethod { get; set; }
        public string DeleteMethod { get; set; }
        public string ListMethod { get; set; }

        public MasterDetailChildDefinition()
        {
            CreateMethod = "Create";
            UpdateMethod = "Update";
            DeleteMethod = "Delete";
            ListMethod = "List";
        }
    }

    public sealed class ListClickTabNavigationDefinition
    {
        public string SourceView { get; set; }
        public string TargetTab { get; set; }
    }

    public sealed class FormTabDefinition
    {
        public string Name { get; set; }
        public List<string> Views { get; set; }
        public WorklistDefinition Worklist { get; set; }

        public FormTabDefinition() { Views = new List<string>(); }
    }

    public sealed class WorklistDefinition
    {
        public int Rows { get; set; }
        public int RefreshIntervalSeconds { get; set; }
        public bool ShowToolbar { get; set; }
        public bool ShowFilter { get; set; }
        public bool ShowSearch { get; set; }
        public bool EnableSearch { get; set; }
        public string Height { get; set; }
        public bool OpenTaskInNewWindow { get; set; }
        public List<string> Actions { get; set; }

        public WorklistDefinition()
        {
            Rows = 20;
            RefreshIntervalSeconds = 300;
            ShowToolbar = true;
            ShowFilter = true;
            EnableSearch = true;
            Height = "445px";
            OpenTaskInNewWindow = true;
            Actions = new List<string> { "viewWorkflow", "sleep", "redirect", "release", "share" };
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
        public CascadeDefinition Cascade { get; set; }
    }

    public sealed class CascadeDefinition
    {
        public string ParentProperty { get; set; }
        public string ParentJoinProperty { get; set; }
        public string ChildJoinProperty { get; set; }
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
