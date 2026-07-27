using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Xml.Linq;
using SourceCode.Categories.Client;
using SourceCode.Forms.Authoring;
using SourceCode.Forms.Management;
using SourceCode.Forms.Utilities;
using SourceCode.Hosting.Client.BaseAPI;
using SourceCode.SmartObjects.Client;
using AuthoringViewType = SourceCode.Forms.Authoring.ViewType;
using ManagementValidationPattern = SourceCode.Forms.Management.ValidationPattern;

namespace K2SmartFormsCli
{
    internal sealed class SmartFormsManager
    {
        private readonly SmartFormsManifest _manifest;
        private IDictionary<string, LookupRuntimeSource> _lookupSources;

        public SmartFormsManager(SmartFormsManifest manifest)
        {
            _manifest = manifest;
        }

        public void ListControlTypes()
        {
            WithFormsManager(delegate(FormsManager manager)
            {
                foreach (var control in manager.GetControlTypes().ControlTypes.Cast<ControlTypeInfo>().OrderBy(x => x.Name))
                    Console.WriteLine(control.Name + "\t" + control.FullName);
                return 0;
            });
        }

        public void DescribeControlType(string name)
        {
            WithFormsManager(delegate(FormsManager manager)
            {
                var control = manager.GetControlTypes().ControlTypes.Cast<ControlTypeInfo>()
                    .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (control == null) throw new CliException("K2 control type not found: " + name);
                Console.WriteLine(control.Name + "\t" + control.FullName);
                foreach (var property in control.GetType().GetProperties().OrderBy(x => x.Name))
                {
                    object value;
                    try { value = property.GetValue(control, null); }
                    catch { continue; }
                    Console.WriteLine("  " + property.Name + " = " + (value == null ? "<null>" : value.ToString()));
                }
                return 0;
            });
        }

        public void FindViewsUsingControl(string controlType)
        {
            WithFormsManager(delegate(FormsManager manager)
            {
                var matches = 0;
                foreach (var view in manager.GetViews().Views.Cast<ViewInfo>().OrderBy(x => x.CategoryPath).ThenBy(x => x.Name))
                {
                    XDocument document;
                    try { document = XDocument.Parse(manager.GetViewDefinition(view.Guid)); }
                    catch { continue; }
                    var count = document.Descendants().Count(x => x.Name.LocalName == "Control" &&
                        string.Equals((string)x.Attribute("Type"), controlType, StringComparison.OrdinalIgnoreCase));
                    if (count == 0) continue;
                    matches++;
                    Console.WriteLine(view.Name + "\t" + view.Guid + "\t" + view.CategoryPath + "\t" + count);
                }
                Console.WriteLine("Matched views: " + matches);
                return 0;
            });
        }

        public void PrintViewDefinition(string name)
        {
            WithFormsManager(delegate(FormsManager manager)
            {
                Guid id;
                var byId = Guid.TryParse(name, out id);
                if (byId ? !manager.CheckViewExists(id) : !manager.CheckViewExists(name))
                    throw new CliException("K2 View not found: " + name);
                var view = byId ? manager.GetView(id) : manager.GetView(name);
                Console.WriteLine(manager.GetViewDefinition(view.Guid));
                return 0;
            });
        }

        public void PrintFormDefinition(string name)
        {
            WithFormsManager(delegate(FormsManager manager)
            {
                if (!manager.CheckFormExists(name)) throw new CliException("K2 Form not found: " + name);
                var form = manager.GetForm(name);
                Console.WriteLine(manager.GetFormDefinition(form.Guid));
                return 0;
            });
        }

        public void PrintViewControlDefinition(string viewName, string controlType)
        {
            WithFormsManager(delegate(FormsManager manager)
            {
                if (!manager.CheckViewExists(viewName)) throw new CliException("K2 View not found: " + viewName);
                var view = manager.GetView(viewName);
                var document = XDocument.Parse(manager.GetViewDefinition(view.Guid));
                var controls = document.Descendants().Where(x => x.Name.LocalName == "Control" &&
                    string.Equals((string)x.Attribute("Type"), controlType, StringComparison.OrdinalIgnoreCase)).ToList();
                if (controls.Count == 0) throw new CliException("View '" + viewName + "' has no control of type '" + controlType + "'.");
                foreach (var control in controls)
                {
                    Console.WriteLine(control.ToString());
                    var id = (string)control.Attribute("ID");
                    foreach (var rule in document.Descendants().Where(x => x.Name.LocalName == "Event" &&
                        x.DescendantsAndSelf().Any(d => string.Equals((string)d.Attribute("SourceID"), id, StringComparison.OrdinalIgnoreCase) ||
                                                       string.Equals((string)d.Attribute("TargetID"), id, StringComparison.OrdinalIgnoreCase))))
                        Console.WriteLine(rule.ToString());
                }
                return 0;
            });
        }

        public void CheckConnectionAndInputs()
        {
            WithFormsManager(delegate(FormsManager manager)
            {
                var elapsed = manager.Ping();
                var themes = manager.GetThemes().Themes.Cast<Theme>().Select(x => x.Name).OrderBy(x => x).ToList();
                if (!themes.Contains(_manifest.Application.Theme, StringComparer.OrdinalIgnoreCase))
                    throw new CliException("K2 theme not found: " + _manifest.Application.Theme + ". Available: " + string.Join(", ", themes.ToArray()));
                var styleProfile = ResolveStyleProfile(manager);
                var commonHeader = ResolveCommonHeader(manager);
                var worklistForms = _manifest.Application.Forms.Where(x => x.Tabs.Any(t => t.Worklist != null)).ToList();
                if (worklistForms.Count > 0)
                {
                    var worklistControl = manager.GetControlTypes().ControlTypes.Cast<ControlTypeInfo>().FirstOrDefault(x => string.Equals(x.Name, "Worklist", StringComparison.OrdinalIgnoreCase));
                    if (worklistControl == null)
                        throw new CliException("The native K2 Worklist control is not registered; required by form(s): " + string.Join(", ", worklistForms.Select(x => x.Name).ToArray()));
                    Console.WriteLine("K2 control input: OK (Worklist, " + worklistControl.FullName + ")");
                }
                Console.WriteLine("K2 SmartForms connection: OK (" + elapsed.TotalMilliseconds.ToString("0") + " ms, theme " + _manifest.Application.Theme + ", availableStyleProfile=" + (styleProfile == null ? "none" : styleProfile.DisplayName + " [" + styleProfile.Name + "]") + ")");
                Console.WriteLine("K2 common framework input: " + (commonHeader == null ? "none requested" : commonHeader.DisplayName + " [" + commonHeader.ViewName + "] from " + commonHeader.CategoryPath + "; footer=" + (commonHeader.Footer == null ? "none" : commonHeader.Footer.ViewName) + "; server-load transfers=" + commonHeader.ServerLoadControlTransfers.Count));
                return 0;
            });

            var lookupSources = LoadLookupRuntimeSources();
            WithSmartObjectServer(delegate(SmartObjectClientServer server)
            {
                foreach (var view in _manifest.Application.Views)
                {
                    SmartObject smartObject;
                    try
                    {
                        smartObject = server.GetSmartObject(view.SmartObject);
                    }
                    catch (Exception ex)
                    {
                        throw new CliException("SmartObject '" + view.SmartObject + "' for view '" + view.Name + "' is unavailable: " + ex.Message);
                    }

                    var properties = new HashSet<string>(smartObject.Properties.Cast<SmartProperty>().Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
                    foreach (var property in view.Properties)
                        if (!properties.Contains(property)) throw new CliException("SmartObject '" + view.SmartObject + "' has no property '" + property + "' requested by view '" + view.Name + "'.");

                    var methods = new HashSet<string>(smartObject.AllMethods.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
                    foreach (var method in view.Methods)
                        if (!methods.Contains(method)) throw new CliException("SmartObject '" + view.SmartObject + "' has no method '" + method + "' requested by view '" + view.Name + "'.");
                    if (!string.IsNullOrWhiteSpace(view.DefaultListMethod) && !methods.Contains(view.DefaultListMethod))
                        throw new CliException("SmartObject '" + view.SmartObject + "' has no default List method '" + view.DefaultListMethod + "' requested by view '" + view.Name + "'.");

                    var externallySupplied = _manifest.Application.Forms
                        .Where(f => f.MasterDetail != null)
                        .SelectMany(f => f.MasterDetail.Details)
                        .Where(d => string.Equals(d.View, view.Name, StringComparison.OrdinalIgnoreCase))
                        .Select(d => d.ForeignKeyProperty)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    foreach (var property in externallySupplied)
                        if (!properties.Contains(property)) throw new CliException("SmartObject '" + view.SmartObject + "' has no master-detail foreign key property '" + property + "' requested by view '" + view.Name + "'.");
                    ValidateRequiredMethodInputs(view, smartObject, externallySupplied);
                    foreach (var binding in view.LookupControls)
                    {
                        var targetProperty = smartObject.Properties.Cast<SmartProperty>().Single(x => string.Equals(x.Name, binding.Property, StringComparison.OrdinalIgnoreCase));
                        var source = lookupSources[binding.Lookup];
                        if (!AreLookupTypesCompatible(targetProperty.Type.ToString(), source.ValuePropertyType))
                            throw new CliException("View '" + view.Name + "' lookup property '" + binding.Property + "' type " + targetProperty.Type + " does not match lookup '" + binding.Lookup + "' value property type " + source.ValuePropertyType + ".");
                        if (binding.Cascade != null)
                        {
                            var parentBinding = view.LookupControls.SingleOrDefault(x => string.Equals(x.Property, binding.Cascade.ParentProperty, StringComparison.OrdinalIgnoreCase));
                            if (parentBinding == null)
                                throw new CliException("View '" + view.Name + "' cascading lookup parent '" + binding.Cascade.ParentProperty + "' must also be declared in lookupControls.");
                            var parentSource = lookupSources[parentBinding.Lookup];
                            if (!parentSource.PropertyNames.Contains(binding.Cascade.ParentJoinProperty))
                                throw new CliException("Cascading lookup parent source '" + parentBinding.Lookup + "' has no join property '" + binding.Cascade.ParentJoinProperty + "'.");
                            if (!source.PropertyNames.Contains(binding.Cascade.ChildJoinProperty))
                                throw new CliException("Cascading lookup child source '" + binding.Lookup + "' has no join property '" + binding.Cascade.ChildJoinProperty + "'.");
                        }
                    }

                    Console.WriteLine("SmartObject input: OK (" + view.SmartObject + ", " + properties.Count + " properties, " + methods.Count + " methods, " + view.LookupControls.Count + " lookup control(s))");
                }
                return 0;
            });
        }

        private static bool AreLookupTypesCompatible(string target, string source)
        {
            if (string.Equals(target, source, StringComparison.OrdinalIgnoreCase)) return true;
            var pair = new HashSet<string>(new[] { target, source }, StringComparer.OrdinalIgnoreCase);
            return pair.SetEquals(new[] { "Number", "Autonumber" }) || pair.SetEquals(new[] { "Guid", "AutoGuid" });
        }

        private IDictionary<string, LookupRuntimeSource> LoadLookupRuntimeSources()
        {
            if (_lookupSources != null) return _lookupSources;
            return WithSmartObjectServer(delegate(SmartObjectClientServer server)
            {
                var result = new Dictionary<string, LookupRuntimeSource>(StringComparer.OrdinalIgnoreCase);
                foreach (var source in _manifest.Application.Lookups)
                {
                    SmartObject smartObject;
                    try { smartObject = server.GetSmartObject(source.SmartObject); }
                    catch (Exception ex) { throw new CliException("Lookup SmartObject '" + source.SmartObject + "' is unavailable: " + ex.Message); }

                    var method = smartObject.ListMethods.Cast<SmartListMethod>().FirstOrDefault(x => string.Equals(x.Name, source.Method, StringComparison.OrdinalIgnoreCase));
                    if (method == null) throw new CliException("Lookup '" + source.Name + "' SmartObject has no List method '" + source.Method + "'.");
                    if (method.RequiredProperties.Count > 0 || method.Parameters.Count > 0)
                        throw new CliException("Lookup '" + source.Name + "' method '" + source.Method + "' must be parameterless for automatic dropdown loading.");
                    var valueProperty = smartObject.Properties.Cast<SmartProperty>().FirstOrDefault(x => string.Equals(x.Name, source.ValueProperty, StringComparison.OrdinalIgnoreCase));
                    if (valueProperty == null) throw new CliException("Lookup '" + source.Name + "' has no value property '" + source.ValueProperty + "'.");
                    var displayProperty = smartObject.Properties.Cast<SmartProperty>().FirstOrDefault(x => string.Equals(x.Name, source.DisplayProperty, StringComparison.OrdinalIgnoreCase));
                    if (displayProperty == null) throw new CliException("Lookup '" + source.Name + "' has no display property '" + source.DisplayProperty + "'.");

                    result[source.Name] = new LookupRuntimeSource
                    {
                        Name = source.Name,
                        SmartObjectGuid = smartObject.Guid,
                        SmartObjectSystemName = smartObject.Name,
                        SmartObjectDisplayName = smartObject.Metadata == null ? smartObject.Name : smartObject.Metadata.DisplayName,
                        MethodName = method.Name,
                        MethodDisplayName = method.Metadata == null ? method.Name : method.Metadata.DisplayName,
                        ValuePropertyName = valueProperty.Name,
                        ValuePropertyDisplayName = valueProperty.Metadata == null ? valueProperty.Name : valueProperty.Metadata.DisplayName,
                        ValuePropertyType = valueProperty.Type.ToString(),
                        DisplayPropertyName = displayProperty.Name,
                        DisplayPropertyDisplayName = displayProperty.Metadata == null ? displayProperty.Name : displayProperty.Metadata.DisplayName,
                        DisplayPropertyType = displayProperty.Type.ToString()
                        ,PropertyNames = new HashSet<string>(smartObject.Properties.Cast<SmartProperty>().Select(x => x.Name), StringComparer.OrdinalIgnoreCase)
                    };
                    var lookupTextContracts = _manifest.Application.Views.SelectMany(view =>
                        view.LookupControls.Where(binding =>
                            string.Equals(binding.Lookup, source.Name, StringComparison.OrdinalIgnoreCase))
                        .Select(binding => new KeyValuePair<string, FieldValidationDefinition>(
                            view.Name + "." + binding.Property,
                            view.Validations.SingleOrDefault(validation =>
                                string.Equals(validation.Property, binding.Property, StringComparison.OrdinalIgnoreCase)))))
                        .Where(x => FieldValidationDefinitionXml.HasTextConstraint(x.Value)).ToList();
                    System.Data.DataTable table;
                    try
                    {
                        smartObject.MethodToExecute = method.Name;
                        table = server.ExecuteListDataTable(smartObject, 1,
                            lookupTextContracts.Count == 0 ? 1 : 10001);
                    }
                    catch (Exception ex)
                    {
                        throw new CliException("Lookup '" + source.Name + "' List execution failed for " + smartObject.Name + "." + method.Name + ": " + ex.Message);
                    }
                    if (lookupTextContracts.Count > 0 && table.Rows.Count > 10000)
                        throw new CliException("Lookup '" + source.Name +
                            "' returned more than 10000 values; bounded-value validation cannot prove the complete dropdown domain.");
                    foreach (var contract in lookupTextContracts)
                    {
                        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                        {
                            var candidate = ReadSampleValue(table.Rows[rowIndex], valueProperty.Name);
                            if (!FieldValidationDefinitionXml.SatisfiesTextConstraint(candidate, contract.Value))
                                throw new CliException("Lookup '" + source.Name + "' value row " + (rowIndex + 1) +
                                    " violates the declared text constraint for " + contract.Key + ".");
                        }
                    }
                    if (table.Rows.Count > 0)
                    {
                        var runtime = result[source.Name];
                        runtime.SampleValue = ReadSampleValue(table.Rows[0], valueProperty.Name);
                        runtime.SampleDisplayValue = ReadSampleValue(table.Rows[0], displayProperty.Name);
                    }
                    Console.WriteLine("Lookup source: OK (" + source.Name + " <= " + smartObject.Name + "." + method.Name +
                        ", value=" + valueProperty.Name + ", display=" + displayProperty.Name +
                        (lookupTextContracts.Count == 0 ? ", sampleRows=" : ", validatedRows=") + table.Rows.Count + ")");
                }
                _lookupSources = result;
                return _lookupSources;
            });
        }

        private static void ValidateRequiredMethodInputs(ViewDefinition view, SmartObject smartObject, IEnumerable<string> externallySuppliedProperties)
        {
            if (view.Type != "capture" && view.Type != "capture-list") return;

            var effectiveProperties = new HashSet<string>(view.Properties, StringComparer.OrdinalIgnoreCase);
            effectiveProperties.UnionWith(externallySuppliedProperties ?? Enumerable.Empty<string>());
            if (view.Options.Contains("all-properties", StringComparer.OrdinalIgnoreCase))
                effectiveProperties.UnionWith(smartObject.Properties.Cast<SmartProperty>().Select(x => x.Name));

            var selectedMethods = view.Options.Contains("all-methods", StringComparer.OrdinalIgnoreCase)
                ? smartObject.AllMethods.ToList()
                : smartObject.AllMethods.Where(x => view.Methods.Contains(x.Name, StringComparer.OrdinalIgnoreCase)).ToList();

            foreach (var method in selectedMethods)
            {
                ValidateRequiredReadOnlyCreateInputs(view, method.Name, method.Type.ToString(),
                    method.RequiredProperties.Cast<SmartProperty>().Select(x => x.Name), externallySuppliedProperties);
                var missing = method.RequiredProperties.Cast<SmartProperty>()
                    .Select(x => x.Name)
                    .Where(x => !effectiveProperties.Contains(x))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (missing.Count == 0) continue;

                throw new CliException(
                    "View '" + view.Name + "' selects method '" + method.Name + "' on SmartObject '" + view.SmartObject +
                    "' but omits required input properties: " + string.Join(", ", missing.ToArray()) +
                    ". Add them to view.properties, use the all-properties option, or change the SmartObject method contract so those values are supplied outside the generated form. " +
                    "SQL DEFAULT constraints do not make generated K2 method inputs optional.");
            }
        }

        internal static void ValidateRequiredReadOnlyCreateInputs(ViewDefinition view, string methodName, string methodType,
            IEnumerable<string> requiredProperties, IEnumerable<string> externallySuppliedProperties)
        {
            if (!string.Equals(methodType, "Create", StringComparison.OrdinalIgnoreCase)) return;
            var externallySupplied = new HashSet<string>(externallySuppliedProperties ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var defaults = new HashSet<string>(view.DefaultValues.Keys, StringComparer.OrdinalIgnoreCase);
            var unsafeProperties = requiredProperties
                .Where(x => view.ReadOnlyProperties.Contains(x, StringComparer.OrdinalIgnoreCase))
                .Where(x => !externallySupplied.Contains(x) && !defaults.Contains(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (unsafeProperties.Count == 0) return;
            throw new CliException(
                "View '" + view.Name + "' selects Create method '" + methodName + "' on SmartObject '" + view.SmartObject +
                "' but required input properties are read-only without a supplied value: " + string.Join(", ", unsafeProperties.ToArray()) +
                ". Make them editable, add literal view.defaultValues, or supply the property through form.masterDetail. SQL DEFAULT constraints do not populate generated K2 method inputs.");
        }

        public IList<ArtifactState> GetArtifactStates()
        {
            return WithFormsManager(delegate(FormsManager manager)
            {
                var result = new List<ArtifactState>();
                foreach (var view in _manifest.Application.Views)
                {
                    if (!manager.CheckViewExists(view.Name))
                    {
                        result.Add(ArtifactState.Absent("View", view.Name));
                        continue;
                    }
                    var info = manager.GetView(view.Name);
                    result.Add(new ArtifactState
                    {
                        Kind = "View",
                        Name = info.Name,
                        Exists = true,
                        Guid = info.Guid,
                        CategoryPath = info.CategoryPath,
                        Version = info.Version,
                        CheckedOut = info.IsCheckedOut,
                        Type = info.Type.ToString()
                    });
                }
                foreach (var form in _manifest.Application.Forms)
                {
                    if (!manager.CheckFormExists(form.Name))
                    {
                        result.Add(ArtifactState.Absent("Form", form.Name));
                        continue;
                    }
                    var info = manager.GetForm(form.Name);
                    var definition = manager.GetFormDefinition(info.Guid);
                    var useLegacyTheme = FormThemeDefinition.ReadUseLegacyTheme(definition);
                    var styleProfile = FormThemeDefinition.ReadStyleProfile(definition);
                    result.Add(new ArtifactState
                    {
                        Kind = "Form",
                        Name = info.Name,
                        Exists = true,
                        Guid = info.Guid,
                        CategoryPath = info.CategoryPath,
                        Version = info.Version,
                        CheckedOut = info.IsCheckedOut,
                        Type = info.Type.ToString(),
                        UseLegacyTheme = useLegacyTheme,
                        StyleProfile = styleProfile == null ? null : styleProfile.Name
                    });
                }
                return result;
            });
        }

        public IDictionary<string, IList<string>> GetExternalDependencies()
        {
            return WithFormsManager(delegate(FormsManager manager)
            {
                var declaredForms = new HashSet<string>(_manifest.Application.Forms.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
                var result = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var view in _manifest.Application.Views)
                {
                    if (view.ReuseExisting) continue;
                    if (!manager.CheckViewExists(view.Name)) continue;
                    var info = manager.GetView(view.Name);
                    var external = manager.GetFormsForView(info.Guid).Forms.Cast<FormInfo>()
                        .Select(x => x.Name)
                        .Where(x => !declaredForms.Contains(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList();
                    if (external.Count > 0) result[view.Name] = external;
                }
                return result;
            });
        }

        public void CheckInForm(string formName)
        {
            if (!_manifest.Application.Forms.Any(x => string.Equals(x.Name, formName, StringComparison.OrdinalIgnoreCase)))
                throw new CliException("Form is not declared in application.forms: " + formName);

            WithFormsManager(delegate(FormsManager manager)
            {
                if (!manager.CheckFormExists(formName)) throw new CliException("K2 Form does not exist: " + formName);
                var info = manager.GetForm(formName);
                if (!info.IsCheckedOut)
                {
                    Console.WriteLine("Form: already checked in (" + info.Name + ", " + info.Guid + ", v" + info.Version + ")");
                    return 0;
                }

                Console.WriteLine("Form: checking in (" + info.Name + ", " + info.Guid + ", v" + info.Version + ", checkedOutBy=" + info.CheckedOutBy + ")");
                manager.CheckInForm(info.Guid);
                var checkedIn = manager.GetForm(info.Guid);
                if (checkedIn.IsCheckedOut) throw new CliException("K2 Form remains checked out after CheckInForm: " + formName);
                Console.WriteLine("Form: checked in (" + checkedIn.Name + ", " + checkedIn.Guid + ", v" + checkedIn.Version + ")");
                return 0;
            });
        }

        public void ReconcileMasterDetail()
        {
            WithFormsManager(delegate(FormsManager manager)
            {
                var styleProfile = ResolveStyleProfile(manager);
                var commonHeader = ResolveCommonHeader(manager);
                var removableCommonHeader = ResolveCommonHeaderRemovalCandidate(manager);
                foreach (var form in _manifest.Application.Forms)
                {
                    if (!manager.CheckFormExists(form.Name)) throw new CliException("K2 Form does not exist: " + form.Name);
                    var info = manager.GetForm(form.Name);
                    var expectedCategory = _manifest.Application.GetFormCategoryPath(form);
                    if (!string.Equals(info.CategoryPath, expectedCategory, StringComparison.OrdinalIgnoreCase))
                        throw new CliException("Refusing to reconcile Form '" + form.Name + "' in category '" + info.CategoryPath + "'; manifest owns '" + expectedCategory + "'.");
                    if (info.IsCheckedOut && !IsCurrentIdentity(Convert.ToString(info.CheckedOutBy)))
                        throw new CliException("Refusing to reconcile Form '" + form.Name + "' while it is checked out by '" + info.CheckedOutBy + "'.");

                    var original = manager.GetFormDefinition(info.Guid);
                    var reconciled = original;
                    var changes = new List<string>();
                    ResolvedMasterDetailRules relationship = null;
                    if (form.MasterDetail != null)
                    {
                        relationship = ResolvedMasterDetailRules.Resolve(manager, form, _manifest.Application.Views);
                        bool masterDetailChanged;
                        reconciled = MasterDetailRules.ReconcileDetailLoads(reconciled, form, relationship, out masterDetailChanged);
                        if (masterDetailChanged) changes.Add("master-detail rules");
                    }

                    var desiredStyleProfile = UsesStyleProfile(form) ? styleProfile : null;
                    var actualStyleProfile = FormThemeDefinition.ReadStyleProfile(reconciled);
                    if (desiredStyleProfile == null && actualStyleProfile != null)
                    {
                        bool styleChanged;
                        reconciled = FormThemeDefinition.RemoveStyleProfile(reconciled, out styleChanged);
                        if (styleChanged) changes.Add("redundant style profile");
                    }
                    else if (desiredStyleProfile != null &&
                        (actualStyleProfile == null || actualStyleProfile.Guid != desiredStyleProfile.Guid))
                    {
                        reconciled = FormThemeDefinition.SetStyleProfile(reconciled, desiredStyleProfile.Guid, desiredStyleProfile.Name);
                        changes.Add("style profile");
                    }

                    var actualLegacyTheme = FormThemeDefinition.ReadUseLegacyTheme(reconciled);
                    if (!actualLegacyTheme.HasValue || actualLegacyTheme.Value != form.UseLegacyTheme)
                    {
                        reconciled = FormThemeDefinition.SetUseLegacyTheme(reconciled, form.UseLegacyTheme);
                        changes.Add("modern theme mode");
                    }

                    var desiredCommonHeader = SelectCommonHeader(form, commonHeader);
                    bool frameworkChanged;
                    reconciled = FormLayoutDefinition.RemoveFrameworkViews(reconciled,
                        RedundantFrameworkGuids(desiredCommonHeader, removableCommonHeader), out frameworkChanged);
                    if (frameworkChanged) changes.Add("redundant common header/footer");

                    var changed = changes.Count > 0;
                    if (!changed)
                    {
                        Console.WriteLine("Form reconciliation: already converged (" + form.Name + ", v" + info.Version + ")");
                        continue;
                    }

                    var checkedOutHere = !info.IsCheckedOut;
                    try
                    {
                        if (checkedOutHere) manager.CheckOutForm(info.Guid);
                        manager.DeployForms(reconciled, expectedCategory, true);
                    }
                    catch
                    {
                        if (checkedOutHere)
                        {
                            var failed = manager.GetForm(info.Guid);
                            if (failed.IsCheckedOut && IsCurrentIdentity(Convert.ToString(failed.CheckedOutBy)) &&
                                string.Equals(manager.GetFormDefinition(info.Guid), original, StringComparison.Ordinal))
                                manager.UndoFormCheckOut(info.Guid);
                        }
                        throw;
                    }

                    var updated = manager.GetForm(info.Guid);
                    if (updated.Guid != info.Guid) throw new CliException("Form reconciliation changed the Form identity: " + form.Name);
                    if (updated.IsCheckedOut) throw new CliException("K2 Form remains checked out after reconciliation: " + form.Name);
                    if (!string.Equals(updated.CategoryPath, expectedCategory, StringComparison.OrdinalIgnoreCase))
                        throw new CliException("Form reconciliation moved Form '" + form.Name + "' out of its manifest category.");
                    var live = manager.GetFormDefinition(updated.Guid);
                    if (relationship != null) MasterDetailRules.Verify(live, form, ResolvedMasterDetailRules.Resolve(manager, form, _manifest.Application.Views));
                    var liveStyleProfile = FormThemeDefinition.ReadStyleProfile(live);
                    if (desiredStyleProfile == null && liveStyleProfile != null)
                        throw new CliException("Form reconciliation did not remove the redundant style profile: " + form.Name);
                    if (desiredStyleProfile != null && (liveStyleProfile == null || liveStyleProfile.Guid != desiredStyleProfile.Guid))
                        throw new CliException("Form reconciliation did not apply the requested style profile: " + form.Name);
                    FormLayoutDefinition.VerifyFrameworkViewsAbsent(live,
                        RedundantFrameworkGuids(desiredCommonHeader, removableCommonHeader), form.Name);
                    Console.WriteLine("Form reconciliation: updated in place (" + form.Name + ", " + info.Guid + ", v" + info.Version + " -> v" + updated.Version + ", changes=" + string.Join(", ", changes.ToArray()) + ")");
                }
                return 0;
            });
            Verify();
            Console.WriteLine("K2 SmartForms Form reconciliation: OK");
        }

        public void RepairView(string viewName, Guid expectedId, string backupPath)
        {
            var declaredView = _manifest.Application.Views.SingleOrDefault(x =>
                string.Equals(x.Name, viewName, StringComparison.OrdinalIgnoreCase));
            if (declaredView == null)
                throw new CliException("View is not declared in application.views: " + viewName);

            var fullBackupPath = Path.GetFullPath(backupPath);
            if (File.Exists(fullBackupPath))
                throw new CliException("Refusing to overwrite existing View backup: " + fullBackupPath);
            var backupDirectory = Path.GetDirectoryName(fullBackupPath);
            if (string.IsNullOrWhiteSpace(backupDirectory) || !Directory.Exists(backupDirectory))
                throw new CliException("View backup directory does not exist: " + backupDirectory);

            var lookupSources = LoadLookupRuntimeSources();
            WithFormsManager(delegate(FormsManager manager)
            {
                ViewInfo originalInfo;
                if (manager.CheckViewExists(viewName))
                    originalInfo = manager.GetView(viewName);
                else if (manager.CheckViewExists(expectedId))
                {
                    originalInfo = manager.GetView(expectedId);
                    Console.WriteLine("View repair preflight: resolving manifest View by required ID because its live name drifted to '" +
                        originalInfo.Name + "'.");
                }
                else
                    throw new CliException("K2 View does not exist by manifest name or required ID: " + viewName);
                if (originalInfo.Guid != expectedId)
                    throw new CliException("Refusing to repair View '" + viewName + "': live ID " + originalInfo.Guid +
                        " does not match required --expected-id " + expectedId + ".");
                if (originalInfo.IsCheckedOut)
                    throw new CliException("Refusing to repair View '" + viewName + "' while it is checked out by '" +
                        originalInfo.CheckedOutBy + "'. Start from a checked-in View.");

                var expectedCategory = _manifest.Application.GetViewCategoryPath(declaredView);
                if (!string.Equals(originalInfo.CategoryPath, expectedCategory, StringComparison.OrdinalIgnoreCase))
                    throw new CliException("Refusing to repair View '" + viewName + "' in category '" +
                        originalInfo.CategoryPath + "'; manifest owns '" + expectedCategory + "'.");

                var original = manager.GetViewDefinition(originalInfo.Guid);
                var originalDocument = XDocument.Parse(original);
                var originalPrimarySource = PrimarySmartObjectIdentity(originalDocument, viewName);
                var originalDependencies = ViewDependencyIds(manager, originalInfo.Guid);

                PrepareValidationPatterns();
                EnsureValidationPatterns(manager, true);
                string rendered;
                using (var renderer = new AutoGenerator(manager.Connection))
                    rendered = RenderView(renderer, declaredView, lookupSources);
                var repaired = RebaseViewIdentity(rendered, originalInfo.Guid, viewName);
                var repairedDocument = XDocument.Parse(repaired);
                var repairedPrimarySource = PrimarySmartObjectIdentity(repairedDocument, viewName);
                if (!string.Equals(originalPrimarySource, repairedPrimarySource, StringComparison.OrdinalIgnoreCase))
                    throw new CliException("Refusing to repair View '" + viewName +
                        "': generated primary SmartObject binding differs from the live binding.");
                VerifyRenderedView(repaired, declaredView, lookupSources);

                File.WriteAllText(fullBackupPath, original);
                Console.WriteLine("View repair backup: " + fullBackupPath);

                var checkedOutHere = false;
                try
                {
                    manager.CheckOutView(originalInfo.Guid);
                    checkedOutHere = true;
                    manager.DeployViews(repaired, expectedCategory, false);

                    var draftInfo = manager.GetView(originalInfo.Guid);
                    AssertRepairedViewInvariants(manager, draftInfo, originalInfo, expectedCategory,
                        originalDependencies, originalPrimarySource, declaredView, lookupSources, true);

                    manager.CheckInView(originalInfo.Guid);
                    checkedOutHere = false;
                }
                catch
                {
                    if (checkedOutHere)
                    {
                        var failed = manager.GetView(originalInfo.Guid);
                        if (failed.IsCheckedOut && IsCurrentIdentity(Convert.ToString(failed.CheckedOutBy)))
                            manager.UndoViewCheckOut(originalInfo.Guid);
                    }
                    throw;
                }

                var updatedInfo = manager.GetView(originalInfo.Guid);
                AssertRepairedViewInvariants(manager, updatedInfo, originalInfo, expectedCategory,
                    originalDependencies, originalPrimarySource, declaredView, lookupSources, false);
                Console.WriteLine("View repair: updated in place (" + viewName + ", " + originalInfo.Guid +
                    ", v" + originalInfo.Version + " -> v" + updatedInfo.Version + ", dependencies=" +
                    originalDependencies.Count + ", checkedIn=true)");
                return 0;
            });
        }

        private string RenderView(AutoGenerator renderer, ViewDefinition view,
            IDictionary<string, LookupRuntimeSource> lookupSources)
        {
            var viewGenerator = new ViewGenerator(ParseViewType(view.Type), ParseViewOptions(view.Options));
            if (view.Type == "capture" || view.Type == "capture-list")
                viewGenerator.InputProperties.AddRange(view.Properties);
            else
                viewGenerator.DisplayProperties.AddRange(view.Properties);
            viewGenerator.InstanceMethods.AddRange(view.Methods);
            if (!string.IsNullOrWhiteSpace(view.DefaultListMethod))
                viewGenerator.DefaultListMethod = view.DefaultListMethod;

            var generated = renderer.Generate(viewGenerator, view.SmartObject, view.Name);
            var definition = ViewLookupDefinition.Apply(generated.ToXml(), view, lookupSources);
            var masterRelationships = _manifest.Application.Forms.Where(f => f.MasterDetail != null &&
                    string.Equals(f.MasterDetail.MasterView, view.Name, StringComparison.OrdinalIgnoreCase))
                .Select(f => f.MasterDetail).ToList();
            var isMaster = masterRelationships.Count > 0;
            var detailRelationships = _manifest.Application.Forms.Where(f => f.MasterDetail != null)
                .SelectMany(f => f.MasterDetail.Details)
                .Where(d => string.Equals(d.View, view.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            var reviewRelationships = _manifest.Application.Forms.Where(f => f.MasterDetail != null &&
                    f.MasterDetail.Review != null &&
                    string.Equals(f.MasterDetail.Review.View, view.Name, StringComparison.OrdinalIgnoreCase))
                .Select(f => f.MasterDetail.Review).ToList();
            var isDetail = detailRelationships.Count > 0;
            definition = ViewPresentationDefinition.Apply(definition, view, isMaster, isDetail);
            definition = ViewChartLayoutDefinition.Apply(definition, view);
            definition = ViewMetricCardLayoutDefinition.Apply(definition, view);
            definition = ViewLifecycleLayoutDefinition.Apply(definition, view);
            definition = ViewWebComponentLayoutDefinition.Apply(definition, view);
            if (isMaster || isDetail || reviewRelationships.Count > 0)
                definition = MasterDetailRules.ConfigureViewRuleSeams(definition, view.Name,
                    masterRelationships, detailRelationships, reviewRelationships);
            VerifyRenderedView(definition, view, lookupSources);
            return definition;
        }

        private void VerifyRenderedView(string definition, ViewDefinition view, IDictionary<string, LookupRuntimeSource> lookupSources)
        {
            var masterRelationships = _manifest.Application.Forms.Where(f => f.MasterDetail != null &&
                    string.Equals(f.MasterDetail.MasterView, view.Name, StringComparison.OrdinalIgnoreCase))
                .Select(f => f.MasterDetail).ToList();
            var isMaster = masterRelationships.Count > 0;
            var isDetail = _manifest.Application.Forms.Where(f => f.MasterDetail != null)
                .SelectMany(f => f.MasterDetail.Details)
                .Any(d => string.Equals(d.View, view.Name, StringComparison.OrdinalIgnoreCase));
            ViewLookupDefinition.Verify(definition, view, lookupSources);
            if (!HasSpecializedBodyLayout(view))
                ViewPresentationDefinition.Verify(definition, view, isMaster, isDetail);
            ViewWebComponentLayoutDefinition.Verify(definition, view);
            var detailRelationships = _manifest.Application.Forms.Where(f => f.MasterDetail != null)
                .SelectMany(f => f.MasterDetail.Details)
                .Where(d => string.Equals(d.View, view.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            var reviewRelationships = _manifest.Application.Forms.Where(f => f.MasterDetail != null &&
                    f.MasterDetail.Review != null &&
                    string.Equals(f.MasterDetail.Review.View, view.Name, StringComparison.OrdinalIgnoreCase))
                .Select(f => f.MasterDetail.Review).ToList();
            if (detailRelationships.Count > 0)
                MasterDetailRules.VerifyDetailViewLoads(definition, view.Name, detailRelationships);
            if (reviewRelationships.Count > 0)
                MasterDetailRules.VerifyReviewViewRules(definition, view.Name, reviewRelationships);
            if (masterRelationships.Count > 0)
                MasterDetailRules.VerifyMasterViewRules(definition, view.Name, masterRelationships);
        }

        internal static string RebaseViewIdentity(string definition, Guid expectedId, string viewName)
        {
            var document = XDocument.Parse(definition, LoadOptions.PreserveWhitespace);
            var view = document.Descendants().SingleOrDefault(x => x.Name.LocalName == "View");
            Guid generatedId;
            if (view == null || !Guid.TryParse((string)view.Attribute("ID"), out generatedId))
                throw new CliException("Generated View '" + viewName + "' has no valid root View ID.");

            var generatedValue = generatedId.ToString();
            var expectedValue = expectedId.ToString();
            foreach (var attribute in document.Descendants().Attributes()
                .Where(x => string.Equals(x.Value, generatedValue, StringComparison.OrdinalIgnoreCase)).ToList())
                attribute.Value = expectedValue;
            foreach (var textNode in document.DescendantNodes().OfType<XText>()
                .Where(x => string.Equals(x.Value, generatedValue, StringComparison.OrdinalIgnoreCase)).ToList())
                textNode.Value = expectedValue;

            var name = view.Elements().SingleOrDefault(x => x.Name.LocalName == "Name");
            var displayName = view.Elements().SingleOrDefault(x => x.Name.LocalName == "DisplayName");
            if (name == null || displayName == null)
                throw new CliException("Generated View '" + viewName + "' has no root Name/DisplayName identity.");
            name.Value = viewName;
            displayName.Value = viewName;

            var rebased = document.ToString(SaveOptions.DisableFormatting);
            if (rebased.IndexOf(generatedValue, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new CliException("Generated View '" + viewName +
                    "' contains an unsupported composite self-reference that could not be identity-rebased.");
            var rebasedDocument = XDocument.Parse(rebased);
            var rebasedView = rebasedDocument.Descendants().Single(x => x.Name.LocalName == "View");
            var viewControls = rebasedDocument.Descendants().Where(x => x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("Type"), "View", StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.Equals((string)rebasedView.Attribute("ID"), expectedValue, StringComparison.OrdinalIgnoreCase) ||
                viewControls.Count != 1 ||
                !string.Equals((string)viewControls[0].Attribute("ID"), expectedValue, StringComparison.OrdinalIgnoreCase))
                throw new CliException("Generated View '" + viewName +
                    "' did not rebase every root View self-reference to " + expectedValue + ".");
            return rebased;
        }

        private static string PrimarySmartObjectIdentity(XDocument document, string viewName)
        {
            var primarySources = document.Descendants().Where(x => x.Name.LocalName == "Source" &&
                string.Equals((string)x.Attribute("SourceType"), "Object", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("ContextType"), "Primary", StringComparison.OrdinalIgnoreCase)).ToList();
            if (primarySources.Count != 1)
                throw new CliException("View '" + viewName + "' must contain exactly one primary SmartObject source; found " +
                    primarySources.Count + ".");
            return ((string)primarySources[0].Attribute("SourceID") ?? string.Empty) + "|" +
                ((string)primarySources[0].Attribute("SourceName") ?? string.Empty);
        }

        private static IList<Guid> ViewDependencyIds(FormsManager manager, Guid viewId)
        {
            return manager.GetFormsForView(viewId).Forms.Cast<FormInfo>().Select(x => x.Guid)
                .Distinct().OrderBy(x => x).ToList();
        }

        private void AssertRepairedViewInvariants(FormsManager manager, ViewInfo actual, ViewInfo original,
            string expectedCategory, IList<Guid> expectedDependencies, string expectedPrimarySource,
            ViewDefinition declaredView, IDictionary<string, LookupRuntimeSource> lookupSources, bool expectCheckedOut)
        {
            if (actual.Guid != original.Guid)
                throw new CliException("View repair changed the View identity: " + declaredView.Name);
            if (!string.Equals(actual.Name, declaredView.Name, StringComparison.Ordinal) ||
                !string.Equals(actual.DisplayName, declaredView.Name, StringComparison.Ordinal))
                throw new CliException("View repair changed the View name/display name for '" + declaredView.Name +
                    "' to '" + actual.Name + "'/'" + actual.DisplayName + "'.");
            if (!string.Equals(actual.CategoryPath, expectedCategory, StringComparison.OrdinalIgnoreCase))
                throw new CliException("View repair moved View '" + declaredView.Name + "' out of its manifest category.");
            if (actual.IsCheckedOut != expectCheckedOut)
                throw new CliException("View repair left View '" + declaredView.Name + "' checkout state at " +
                    actual.IsCheckedOut + ", expected " + expectCheckedOut + ".");

            var dependencies = ViewDependencyIds(manager, actual.Guid);
            if (!dependencies.SequenceEqual(expectedDependencies))
                throw new CliException("View repair changed Form dependencies for View '" + declaredView.Name + "'.");
            var liveDefinition = manager.GetViewDefinition(actual.Guid);
            var liveDocument = XDocument.Parse(liveDefinition);
            if (!string.Equals(PrimarySmartObjectIdentity(liveDocument, declaredView.Name), expectedPrimarySource,
                    StringComparison.OrdinalIgnoreCase))
                throw new CliException("View repair changed the primary SmartObject binding for View '" +
                    declaredView.Name + "'.");
            VerifyRenderedView(liveDefinition, declaredView, lookupSources);
        }

        public void Deploy(bool resume, bool formsOnly)
        {
            CheckConnectionAndInputs();
            var lookupSources = LoadLookupRuntimeSources();
            var states = GetArtifactStates();
            var selectedStates = formsOnly ? states.Where(x => string.Equals(x.Kind, "Form", StringComparison.OrdinalIgnoreCase)).ToList() : states;
            var reusedViews = new HashSet<string>(_manifest.Application.Views.Where(x => x.ReuseExisting).Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            var existing = selectedStates.Where(x => x.Exists &&
                !(string.Equals(x.Kind, "View", StringComparison.OrdinalIgnoreCase) && reusedViews.Contains(x.Name))).ToList();
            var missingReusedViews = _manifest.Application.Views.Where(x => x.ReuseExisting &&
                !states.Any(s => s.Exists && string.Equals(s.Kind, "View", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.Name, x.Name, StringComparison.OrdinalIgnoreCase))).Select(x => x.Name).ToList();
            if (missingReusedViews.Count > 0)
                throw new CliException("Reusable View(s) must already exist before deployment: " +
                    string.Join(", ", missingReusedViews.ToArray()) + ".");
            if (existing.Count > 0 && !_manifest.Application.ReplaceExisting && !resume)
                throw new CliException("Artifact(s) already exist and application.replaceExisting is false: " + string.Join(", ", existing.Select(x => x.Kind + " " + x.Name).ToArray()));

            if (formsOnly)
            {
                var missingViews = _manifest.Application.Views.Where(x => !states.Any(s => s.Exists && string.Equals(s.Kind, "View", StringComparison.OrdinalIgnoreCase) && string.Equals(s.Name, x.Name, StringComparison.OrdinalIgnoreCase))).Select(x => x.Name).ToList();
                if (missingViews.Count > 0)
                    throw new CliException("--forms-only requires every manifest View to exist: " + string.Join(", ", missingViews.ToArray()) + ". Use --resume to create only missing artifacts.");
            }

            IDictionary<string, IList<string>> dependencies = resume || formsOnly
                ? new Dictionary<string, IList<string>>()
                : GetExternalDependencies();
            if (dependencies.Count > 0)
            {
                var details = dependencies.Select(x => x.Key + " -> " + string.Join(", ", x.Value.ToArray()));
                throw new CliException("Cannot replace views used by forms outside this manifest: " + string.Join("; ", details.ToArray()));
            }

            WithFormsManager(delegate(FormsManager manager)
            {
                var styleProfile = ResolveStyleProfile(manager);
                var commonHeader = ResolveCommonHeader(manager);
                var renderedViews = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!formsOnly)
                {
                    PrepareValidationPatterns();
                    using (var renderer = new AutoGenerator(manager.Connection))
                    {
                        foreach (var view in _manifest.Application.Views)
                        {
                            if (view.ReuseExisting) continue;
                            if (resume && manager.CheckViewExists(view.Name)) continue;
                            renderedViews[view.Name] = RenderView(renderer, view, lookupSources);
                        }
                    }
                    Console.WriteLine("Pre-render validation: " + renderedViews.Count + " View definition(s) generated before K2 mutation.");
                    EnsureValidationPatterns(manager, true);
                    using (var renderer = new AutoGenerator(manager.Connection))
                    {
                        foreach (var view in _manifest.Application.Views.Where(x => renderedViews.ContainsKey(x.Name)))
                            renderedViews[view.Name] = RenderView(renderer, view, lookupSources);
                    }
                }
                if (resume)
                    Console.WriteLine("Resume mode: preserving existing manifest artifacts and creating only missing Views/Forms.");
                else if (formsOnly)
                    Console.WriteLine("Forms-only mode: preserving all Views and replacing only declared Forms.");

                if (existing.Count > 0 && !resume)
                {
                    foreach (var form in _manifest.Application.Forms)
                    {
                        if (!manager.CheckFormExists(form.Name)) continue;
                        var info = manager.GetForm(form.Name);
                        manager.DeleteForm(info.Guid);
                        Console.WriteLine("Form: removed for replacement (" + form.Name + ", " + info.Guid + ")");
                    }
                    foreach (var view in formsOnly ? new List<ViewDefinition>() : _manifest.Application.Views)
                    {
                        if (view.ReuseExisting) continue;
                        if (!manager.CheckViewExists(view.Name)) continue;
                        var info = manager.GetView(view.Name);
                        manager.DeleteView(info.Guid);
                        Console.WriteLine("View: removed for replacement (" + view.Name + ", " + info.Guid + ")");
                    }
                    // K2 5.10 keeps deleted Form/View identity metadata for the lifetime of the
                    // current process, not merely the FormsManager/connection. Creating here can
                    // silently allocate a suffixed internal View identity before throwing. Cross
                    // the process boundary before any create so the wrapper's single bounded
                    // --resume pass recreates only the canonical missing identities.
                    throw new CliException(
                        "REPLACEMENT RECOVERY REQUIRED: replacement deletion completed; start one fresh-process missing-artifact recovery pass before creating any Form or View.");
                }

                Action<FormsManager, bool> deployMissingArtifacts = delegate(
                    FormsManager deploymentManager, bool preserveExistingArtifacts)
                {
                    using (var generator = new AutoGenerator(deploymentManager.Connection))
                    {
                        foreach (var view in _manifest.Application.Views)
                        {
                            if (formsOnly) break;
                        if (view.ReuseExisting)
                        {
                                var existingView = deploymentManager.GetView(view.Name);
                                Console.WriteLine("View: reused existing (" + view.Name + ", " + existingView.Guid + ", v" + existingView.Version + ")");
                                continue;
                            }
                            if (preserveExistingArtifacts && deploymentManager.CheckViewExists(view.Name))
                            {
                                var existingView = deploymentManager.GetView(view.Name);
                                Console.WriteLine("View: preserved existing (" + view.Name + ", " + existingView.Guid + ", v" + existingView.Version + ")");
                                continue;
                            }
                            var definition = renderedViews[view.Name];
                            deploymentManager.DeployViews(definition, _manifest.Application.GetViewCategoryPath(view), _manifest.Application.CheckIn);
                            var info = deploymentManager.GetView(view.Name);
                            Console.WriteLine("View: deployed (" + view.Name + ", " + info.Guid + ", " + info.Type + ", category " + info.CategoryPath + ", " + view.LookupControls.Count + " lookup control(s))");
                        }

                        foreach (var form in _manifest.Application.Forms)
                        {
                            if (preserveExistingArtifacts && deploymentManager.CheckFormExists(form.Name))
                            {
                                var existingForm = deploymentManager.GetForm(form.Name);
                                Console.WriteLine("Form: preserved existing (" + form.Name + ", " + existingForm.Guid + ", v" + existingForm.Version + ")");
                                continue;
                            }
                            var formStyleProfile = UsesStyleProfile(form) ? styleProfile : null;
                            var formCommonHeader = SelectCommonHeader(form, commonHeader);
                            var formGenerator = new FormGenerator(ParseFormOptions(form.Options), ParseFormBehaviors(form.Behaviors), _manifest.Application.Theme);
                            var formViews = formCommonHeader == null ? form.Views.ToArray() :
                                new[] { formCommonHeader.ViewName }.Concat(form.Views).Concat(formCommonHeader.Footer == null ? new string[0] : new[] { formCommonHeader.Footer.ViewName }).ToArray();
                            var generated = generator.Generate(formGenerator, formViews, form.Name);
                            var definition = FormThemeDefinition.SetUseLegacyTheme(generated.ToXml(), form.UseLegacyTheme);
                            if (formStyleProfile != null) definition = FormThemeDefinition.SetStyleProfile(definition, formStyleProfile.Guid, formStyleProfile.Name);
                            else
                            {
                                bool ignored;
                                definition = FormThemeDefinition.RemoveStyleProfile(definition, out ignored);
                            }
                            definition = FormLayoutDefinition.Apply(definition, form, formCommonHeader, ResolveHeaderParameters(formCommonHeader, form), ResolveHeaderControlTransfers(formCommonHeader, form));
                            var masterDetail = ResolvedMasterDetailRules.Resolve(deploymentManager, form, _manifest.Application.Views);
                            definition = MasterDetailRules.Apply(definition, form, masterDetail);
                            definition = GuidedJourneyRules.Apply(definition, form, formCommonHeader);
                            var preFill = ResolvedFormPreFill.Resolve(deploymentManager, form, _manifest.Application.Views, lookupSources);
                            definition = FormPreFillRules.Apply(definition, form, preFill);
                            deploymentManager.DeployForms(definition, _manifest.Application.GetFormCategoryPath(form), _manifest.Application.CheckIn);
                            var info = deploymentManager.GetForm(form.Name);
                            Console.WriteLine("Form: deployed (" + form.Name + ", " + info.Guid + ", theme " + info.Theme.Name + ", styleProfile=" + (formStyleProfile == null ? "none" : formStyleProfile.Name) + ", legacyTheme=" + form.UseLegacyTheme.ToString().ToLowerInvariant() + ", commonHeader=" + (formCommonHeader == null ? "none" : formCommonHeader.ViewName) + ", commonFooter=" + (formCommonHeader == null || formCommonHeader.Footer == null ? "none" : formCommonHeader.Footer.ViewName) + ", tabs=" + form.Tabs.Count + ", worklist=" + form.Tabs.Any(x => x.Worklist != null).ToString().ToLowerInvariant() + ", preFill=" + (form.PreFill.EffectiveEnabled ? "test-only" : "disabled") + ")");
                            Console.WriteLine(FormPreFillRules.Errata(form));
                        }
                    }
                };

                deployMissingArtifacts(manager, resume);
                return 0;
            });
        }

        internal static bool IsKnownStaleReplacementFailure(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is NullReferenceException) return true;
                if (string.Equals(current.Message, "Object reference not set to an instance of an object.",
                    StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public void Verify()
        {
            var runtimeForms = new List<string>();
            var lookupSources = LoadLookupRuntimeSources();
            WithFormsManager(delegate(FormsManager manager)
            {
                PrepareValidationPatterns();
                EnsureValidationPatterns(manager, false);
                var expectedStyleProfile = ResolveStyleProfile(manager);
                var commonHeader = ResolveCommonHeader(manager);
                var formControlTypes = manager.GetControlTypes().ControlTypes.Cast<ControlTypeInfo>().ToList();
                var formControlFlags = formControlTypes
                    .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => Convert.ToString(x.First().Flags),
                        StringComparer.OrdinalIgnoreCase);
                var formBooleanControlProperties = formControlTypes
                    .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key,
                        x => ReadBooleanControlProperties(Convert.ToString(x.First().Properties)),
                        StringComparer.OrdinalIgnoreCase);
                var formInitialBooleanControlProperties = formControlTypes
                    .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key,
                        x => ReadInitialBooleanControlProperties(Convert.ToString(x.First().Properties)),
                        StringComparer.OrdinalIgnoreCase);
                foreach (var expected in _manifest.Verification.ExpectedViews)
                {
                    if (!manager.CheckViewExists(expected)) throw new CliException("Expected K2 View is missing: " + expected);
                    var info = manager.GetView(expected);
                    var definition = manager.GetViewDefinition(info.Guid);
                    if (string.IsNullOrWhiteSpace(definition)) throw new CliException("K2 View has an empty definition: " + expected);
                    var declaredView = _manifest.Application.Views.SingleOrDefault(x => string.Equals(x.Name, expected, StringComparison.OrdinalIgnoreCase));
                    if (declaredView == null) throw new CliException("Expected K2 View is not declared in application.views: " + expected);
                    var expectedCategory = _manifest.Application.GetViewCategoryPath(declaredView);
                    if (!string.Equals(info.CategoryPath, expectedCategory, StringComparison.OrdinalIgnoreCase))
                        throw new CliException("K2 View is in category '" + info.CategoryPath + "', expected '" + expectedCategory + "': " + expected);
                    if (_manifest.Application.CheckIn && info.IsCheckedOut) throw new CliException("K2 View remains checked out: " + expected);
                    ViewLookupDefinition.Verify(definition, declaredView, lookupSources);
                    var masterRelationships = _manifest.Application.Forms.Where(f => f.MasterDetail != null &&
                            string.Equals(f.MasterDetail.MasterView, declaredView.Name, StringComparison.OrdinalIgnoreCase))
                        .Select(f => f.MasterDetail).ToList();
                    var isMaster = masterRelationships.Count > 0;
                    var detailRelationships = _manifest.Application.Forms.Where(f => f.MasterDetail != null)
                        .SelectMany(f => f.MasterDetail.Details).Where(d => string.Equals(d.View, declaredView.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                    var reviewRelationships = _manifest.Application.Forms.Where(f => f.MasterDetail != null &&
                            f.MasterDetail.Review != null &&
                            string.Equals(f.MasterDetail.Review.View, declaredView.Name, StringComparison.OrdinalIgnoreCase))
                        .Select(f => f.MasterDetail.Review).ToList();
                    var isDetail = detailRelationships.Count > 0;
                    if (!HasSpecializedBodyLayout(declaredView))
                        ViewPresentationDefinition.Verify(definition, declaredView, isMaster, isDetail);
                    ViewChartLayoutDefinition.Verify(definition, declaredView);
                    ViewMetricCardLayoutDefinition.Verify(definition, declaredView);
                    ViewLifecycleLayoutDefinition.Verify(definition, declaredView);
                    ViewWebComponentLayoutDefinition.Verify(definition, declaredView);
                    if (isDetail) MasterDetailRules.VerifyDetailViewLoads(definition, declaredView.Name, detailRelationships);
                    if (reviewRelationships.Count > 0)
                        MasterDetailRules.VerifyReviewViewRules(definition, declaredView.Name, reviewRelationships);
                    if (masterRelationships.Count > 0)
                        MasterDetailRules.VerifyMasterViewRules(definition, declaredView.Name, masterRelationships);
                    VerifyDesignerAuthoringHydration(definition, expected, false);
                    Console.WriteLine("View verification: OK (" + expected + ", " + info.Guid + ", v" + info.Version + ", " + info.Type + ")");
                }

                foreach (var expected in _manifest.Verification.ExpectedForms)
                {
                    if (!manager.CheckFormExists(expected)) throw new CliException("Expected K2 Form is missing: " + expected);
                    var info = manager.GetForm(expected);
                    var definition = manager.GetFormDefinition(info.Guid);
                    if (string.IsNullOrWhiteSpace(definition)) throw new CliException("K2 Form has an empty definition: " + expected);
                    VerifyDesignerAuthoringHydration(definition, expected, true, formControlFlags,
                        formBooleanControlProperties, formInitialBooleanControlProperties);
                    var declaredForm = _manifest.Application.Forms.SingleOrDefault(x => string.Equals(x.Name, expected, StringComparison.OrdinalIgnoreCase));
                    if (declaredForm == null) throw new CliException("Expected K2 Form is not declared in application.forms: " + expected);
                    var expectedCategory = _manifest.Application.GetFormCategoryPath(declaredForm);
                    if (!string.Equals(info.CategoryPath, expectedCategory, StringComparison.OrdinalIgnoreCase))
                        throw new CliException("K2 Form is in category '" + info.CategoryPath + "', expected '" + expectedCategory + "': " + expected);
                    if (_manifest.Application.CheckIn && info.IsCheckedOut) throw new CliException("K2 Form remains checked out: " + expected);
                    var useLegacyTheme = FormThemeDefinition.ReadUseLegacyTheme(definition);
                    if (!useLegacyTheme.HasValue)
                        throw new CliException("K2 Form does not explicitly set UseLegacyTheme: " + expected);
                    if (useLegacyTheme.Value != declaredForm.UseLegacyTheme)
                        throw new CliException("K2 Form UseLegacyTheme is " + useLegacyTheme.Value.ToString().ToLowerInvariant() + ", expected " + declaredForm.UseLegacyTheme.ToString().ToLowerInvariant() + ": " + expected);
                    var actualStyleProfile = FormThemeDefinition.ReadStyleProfile(definition);
                    var formStyleProfile = UsesStyleProfile(declaredForm) ? expectedStyleProfile : null;
                    var formCommonHeader = SelectCommonHeader(declaredForm, commonHeader);
                    if (formStyleProfile == null && actualStyleProfile != null)
                        throw new CliException("K2 Form has style profile '" + actualStyleProfile.Name + "' but the manifest expects none: " + expected);
                    if (formStyleProfile != null && (actualStyleProfile == null || actualStyleProfile.Guid != formStyleProfile.Guid))
                        throw new CliException("K2 Form style profile does not match '" + formStyleProfile.DisplayName + "' [" + formStyleProfile.Name + "]: " + expected);
                    FormLayoutDefinition.Verify(definition, declaredForm, formCommonHeader, ResolveHeaderParameters(formCommonHeader, declaredForm), ResolveHeaderControlTransfers(formCommonHeader, declaredForm));
                    MasterDetailRules.Verify(definition, declaredForm, ResolvedMasterDetailRules.Resolve(manager, declaredForm, _manifest.Application.Views));
                    GuidedJourneyRules.Verify(definition, declaredForm, formCommonHeader);
                    var preFill = ResolvedFormPreFill.Resolve(manager, declaredForm, _manifest.Application.Views, lookupSources);
                    FormPreFillRules.Verify(definition, declaredForm, preFill);
                    foreach (var viewName in declaredForm.Views)
                    {
                        var viewGuid = manager.GetView(viewName).Guid.ToString();
                        if (definition.IndexOf(viewGuid, StringComparison.OrdinalIgnoreCase) < 0)
                            throw new CliException("K2 Form '" + expected + "' does not reference expected view '" + viewName + "'.");
                    }
                    if (formCommonHeader != null && definition.IndexOf(formCommonHeader.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase) < 0)
                        throw new CliException("K2 Form '" + expected + "' does not reference requested common header '" + formCommonHeader.ViewName + "'.");
                    if (formCommonHeader != null && formCommonHeader.Footer != null && definition.IndexOf(formCommonHeader.Footer.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase) < 0)
                        throw new CliException("K2 Form '" + expected + "' does not reference requested common footer '" + formCommonHeader.Footer.ViewName + "'.");
                    Console.WriteLine("Form verification: OK (" + expected + ", " + info.Guid + ", v" + info.Version + ", theme " + info.Theme.Name + ", styleProfile=" + (actualStyleProfile == null ? "none" : actualStyleProfile.Name) + ", legacyTheme=" + useLegacyTheme.Value.ToString().ToLowerInvariant() + ", commonHeader=" + (formCommonHeader == null ? "none" : formCommonHeader.ViewName) + ", commonFooter=" + (formCommonHeader == null || formCommonHeader.Footer == null ? "none" : formCommonHeader.Footer.ViewName) + ", preFill=" + (declaredForm.PreFill.EffectiveEnabled ? "test-only" : "disabled") + ")");
                    Console.WriteLine(FormPreFillRules.Errata(declaredForm));
                    runtimeForms.Add(expected);
                }
                return 0;
            });

            if (_manifest.Verification.SmokeTestRuntime)
            {
                foreach (var form in runtimeForms)
                    SmokeTestRuntime(form);
            }
            Console.WriteLine("K2 SmartForms verification: OK (" + _manifest.Verification.ExpectedViews.Count + " view(s), " + _manifest.Verification.ExpectedForms.Count + " form(s))");
        }

        public void Cleanup(bool manifestOnly, bool deleteRootCategory)
        {
            if (!manifestOnly)
            {
                var dependencies = GetExternalDependencies();
                if (dependencies.Count > 0)
                {
                    var details = dependencies.Select(x => x.Key + " -> " + string.Join(", ", x.Value.ToArray()));
                    throw new CliException("Cannot delete views used by forms outside this manifest: " + string.Join("; ", details.ToArray()));
                }
            }
            else Console.WriteLine("Manifest-only cleanup: skipping environment-wide external Form dependency discovery.");

            WithFormsManager(delegate(FormsManager manager)
            {
                foreach (var form in _manifest.Application.Forms)
                {
                    if (!manager.CheckFormExists(form.Name))
                    {
                        Console.WriteLine("Form: already absent (" + form.Name + ")");
                        continue;
                    }
                    var info = manager.GetForm(form.Name);
                    var expectedCategory = _manifest.Application.GetFormCategoryPath(form);
                    if (!IsOwnedOrOrphanedCategory(info.CategoryPath, expectedCategory, manifestOnly))
                        throw new CliException("Refusing to delete Form '" + form.Name + "' from category '" + info.CategoryPath + "'; manifest owns '" + expectedCategory + "'.");
                    if (!string.Equals(info.CategoryPath, expectedCategory, StringComparison.OrdinalIgnoreCase))
                        Console.WriteLine("Form: expected category link is absent; deleting exact manifest artifact from ancestor category '" + info.CategoryPath + "' (" + form.Name + ")");
                    if (info.IsCheckedOut)
                    {
                        if (!IsCurrentIdentity(Convert.ToString(info.CheckedOutBy)))
                            throw new CliException("Refusing to delete Form '" + form.Name + "' while it is checked out by '" + info.CheckedOutBy + "'.");
                        manager.UndoFormCheckOut(info.Guid);
                        info = manager.GetForm(info.Guid);
                        if (info.IsCheckedOut) throw new CliException("K2 Form remains checked out after discarding the current identity's cleanup draft: " + form.Name);
                        Console.WriteLine("Form: discarded current identity's checkout before deletion (" + form.Name + ")");
                    }
                    manager.DeleteForm(info.Guid);
                    Console.WriteLine("Form: deleted (" + form.Name + ", " + info.Guid + ")");
                }
                foreach (var view in _manifest.Application.Views)
                {
                    if (view.ReuseExisting)
                    {
                        Console.WriteLine("View: preserved reusable dependency (" + view.Name + ")");
                        continue;
                    }
                    if (!manager.CheckViewExists(view.Name))
                    {
                        Console.WriteLine("View: already absent (" + view.Name + ")");
                        continue;
                    }
                    var info = manager.GetView(view.Name);
                    var expectedCategory = _manifest.Application.GetViewCategoryPath(view);
                    if (!IsOwnedOrOrphanedCategory(info.CategoryPath, expectedCategory, manifestOnly))
                        throw new CliException("Refusing to delete View '" + view.Name + "' from category '" + info.CategoryPath + "'; manifest owns '" + expectedCategory + "'.");
                    if (!string.Equals(info.CategoryPath, expectedCategory, StringComparison.OrdinalIgnoreCase))
                        Console.WriteLine("View: expected category link is absent; deleting exact manifest artifact from ancestor category '" + info.CategoryPath + "' (" + view.Name + ")");
                    if (info.IsCheckedOut)
                    {
                        if (!IsCurrentIdentity(Convert.ToString(info.CheckedOutBy)))
                            throw new CliException("Refusing to delete View '" + view.Name + "' while it is checked out by '" + info.CheckedOutBy + "'.");
                        manager.UndoViewCheckOut(info.Guid);
                        info = manager.GetView(info.Guid);
                        if (info.IsCheckedOut) throw new CliException("K2 View remains checked out after discarding the current identity's cleanup draft: " + view.Name);
                        Console.WriteLine("View: discarded current identity's checkout before deletion (" + view.Name + ")");
                    }
                    manager.DeleteView(info.Guid);
                    Console.WriteLine("View: deleted (" + view.Name + ", " + info.Guid + ")");
                }
                DeleteOwnedValidationPatterns(manager);
                return 0;
            });
            CleanupOwnedCategories(deleteRootCategory);
        }

        private void CleanupOwnedCategories(bool deleteRootCategory)
        {
            WithCategoryServer(delegate(CategoryServer server)
            {
                foreach (var path in CleanupCategoryPaths(_manifest.Application, deleteRootCategory))
                    DeleteCategoryIfEmpty(server, path);
                return 0;
            });
        }

        internal static IList<string> CleanupCategoryPaths(ApplicationOptions application, bool deleteRootCategory)
        {
            if (application == null || string.IsNullOrWhiteSpace(application.RootCategoryPath))
                return new List<string>();
            var root = application.RootCategoryPath.Trim().TrimEnd('\\', '/');
            var paths = new List<string>
            {
                root + "\\Admin\\Forms",
                root + "\\Admin\\Views",
                root + "\\Admin",
                root + "\\Forms",
                root + "\\Views"
            };
            if (deleteRootCategory) paths.Add(root);
            return paths;
        }

        private static void DeleteCategoryIfEmpty(CategoryServer server, string path)
        {
            var manager = server.GetCategoryManager(1, true, true);
            var category = manager.Categories.Cast<Category>()
                .FirstOrDefault(x => x != null &&
                    string.Equals(GetCategoryFullPath(x), path, StringComparison.OrdinalIgnoreCase));
            if (category == null)
            {
                Console.WriteLine("K2 category: already absent (" + path + ")");
                return;
            }
            if (category.IsRoot)
                throw new CliException("Refusing to delete a K2 category-system root: " + path);

            if (!category.HasLoadedData) server.LoadCategoryData(category);
            var childCount = category.ChildCategoryIds == null ? 0 : category.ChildCategoryIds.Count;
            var dataCount = category.DataList == null ? 0 : category.DataList.Count;
            if (childCount != 0 || dataCount != 0)
            {
                Console.WriteLine("K2 category: retained (not empty: " + childCount + " child category(s), " + dataCount + " artifact link(s): " + path + ")");
                return;
            }

            server.DeleteCategory(category);
            manager = server.GetCategoryManager(1, true, true);
            if (manager.Categories.Cast<Category>().Any(x => x != null &&
                string.Equals(GetCategoryFullPath(x), path, StringComparison.OrdinalIgnoreCase)))
                throw new CliException("K2 category remains after deletion: " + path);
            Console.WriteLine("K2 category: deleted (" + path + ")");
        }

        private static string GetCategoryFullPath(Category category)
        {
            if (category == null) return null;
            if (string.IsNullOrWhiteSpace(category.Path)) return category.Name;
            if (string.IsNullOrWhiteSpace(category.Name)) return category.Path;
            return category.Path.TrimEnd('\\', '/') + "\\" + category.Name;
        }

        private static bool HasSpecializedBodyLayout(ViewDefinition view)
        {
            return (view.WebComponents != null && view.WebComponents.Count > 0) ||
                (view.Charts != null && view.Charts.Count > 0) ||
                (view.MetricCards != null && view.MetricCards.Count > 0) ||
                (view.LifecycleTrackers != null && view.LifecycleTrackers.Count > 0);
        }

        internal static void VerifyFormControlAvailability(string definition, string name,
            IDictionary<string, string> controlFlags)
        {
            if (controlFlags == null) throw new ArgumentNullException("controlFlags");
            XDocument document;
            try { document = XDocument.Parse(definition); }
            catch (Exception ex)
            {
                throw new CliException("K2 Form '" + name +
                    "' cannot be checked against installed control metadata: " + ex.Message);
            }
            var form = document.Descendants().SingleOrDefault(x =>
                x.Name.LocalName == "Form" && x.Attribute("ID") != null);
            if (form == null)
                throw new CliException("K2 Form '" + name +
                    "' cannot be checked against installed control metadata because its Form element is missing.");
            var controls = form.Elements().FirstOrDefault(x => x.Name.LocalName == "Controls");
            if (controls == null)
                throw new CliException("K2 Form '" + name +
                    "' cannot be checked against installed control metadata because its Controls element is missing.");

            var unavailable = controls.Elements().Where(x => x.Name.LocalName == "Control")
                .Select(x => new
                {
                    Type = (string)x.Attribute("Type"),
                    Name = x.Elements().FirstOrDefault(y => y.Name.LocalName == "Name")
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Type) &&
                    controlFlags.ContainsKey(x.Type) &&
                    (controlFlags[x.Type] ?? string.Empty).IndexOf(
                        "UnavailableOnFormLevel", StringComparison.OrdinalIgnoreCase) >= 0)
                .GroupBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Key + " [" + string.Join(", ", x.Select(y =>
                    y.Name == null || string.IsNullOrWhiteSpace(y.Name.Value)
                        ? "<unnamed>" : y.Name.Value).ToArray()) + "]")
                .ToList();
            if (unavailable.Count > 0)
                throw new CliException("K2 Form '" + name +
                    "' contains control type(s) that installed K2 metadata marks UnavailableOnFormLevel: " +
                    string.Join("; ", unavailable.ToArray()) +
                    ". The Authoring.Form XML round trip does not enforce this Designer placement restriction.");
        }

        internal static ISet<string> ReadBooleanControlProperties(string metadata)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(metadata)) return result;
            XDocument document;
            try { document = XDocument.Parse(metadata); }
            catch { return result; }
            foreach (var property in document.Descendants().Where(x =>
                x.Name.LocalName == "Prop" &&
                string.Equals((string)x.Attribute("type"), "bool", StringComparison.OrdinalIgnoreCase)))
            {
                var id = (string)property.Attribute("ID");
                if (!string.IsNullOrWhiteSpace(id)) result.Add(id);
            }
            return result;
        }

        internal static ISet<string> ReadInitialBooleanControlProperties(string metadata)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(metadata)) return result;
            XDocument document;
            try { document = XDocument.Parse(metadata); }
            catch { return result; }
            foreach (var property in document.Descendants().Where(x =>
                x.Name.LocalName == "Prop" &&
                string.Equals((string)x.Attribute("type"), "bool", StringComparison.OrdinalIgnoreCase)))
            {
                var id = (string)property.Attribute("ID");
                var initial = property.Elements().FirstOrDefault(x => x.Name.LocalName == "InitialValue");
                bool parsed;
                if (!string.IsNullOrWhiteSpace(id) && initial != null &&
                    bool.TryParse(initial.Value, out parsed))
                    result.Add(id);
            }
            return result;
        }

        internal static void VerifyFormBooleanControlProperties(string definition, string name,
            IDictionary<string, ISet<string>> booleanProperties,
            IDictionary<string, ISet<string>> initialBooleanProperties = null)
        {
            if (booleanProperties == null) throw new ArgumentNullException("booleanProperties");
            XDocument document;
            try { document = XDocument.Parse(definition); }
            catch (Exception ex)
            {
                throw new CliException("K2 Form '" + name +
                    "' cannot be checked for Designer Boolean control properties: " + ex.Message);
            }
            var form = document.Descendants().SingleOrDefault(x =>
                x.Name.LocalName == "Form" && x.Attribute("ID") != null);
            if (form == null)
                throw new CliException("K2 Form '" + name +
                    "' cannot be checked for Designer Boolean control properties because its Form root is missing.");
            var controls = form.Elements().FirstOrDefault(x => x.Name.LocalName == "Controls");
            if (controls == null)
                throw new CliException("K2 Form '" + name +
                    "' cannot be checked for Designer Boolean control properties because its Controls element is missing.");

            var invalid = new List<string>();
            foreach (var control in controls.Elements().Where(x => x.Name.LocalName == "Control"))
            {
                var type = (string)control.Attribute("Type");
                ISet<string> declaredBooleanProperties;
                if (string.IsNullOrWhiteSpace(type) ||
                    !booleanProperties.TryGetValue(type, out declaredBooleanProperties) ||
                    declaredBooleanProperties == null || declaredBooleanProperties.Count == 0) continue;
                var controlNameElement = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Name");
                var controlName = controlNameElement == null || string.IsNullOrWhiteSpace(controlNameElement.Value)
                    ? "<unnamed>" : controlNameElement.Value;
                var properties = control.Elements().FirstOrDefault(x => x.Name.LocalName == "Properties");
                var propertyElements = properties == null
                    ? new List<XElement>()
                    : properties.Elements().Where(x => x.Name.LocalName == "Property").ToList();
                ISet<string> requiredInitialProperties;
                if (initialBooleanProperties != null &&
                    initialBooleanProperties.TryGetValue(type, out requiredInitialProperties) &&
                    requiredInitialProperties != null)
                {
                    foreach (var required in requiredInitialProperties)
                    {
                        if (!propertyElements.Any(x =>
                            string.Equals((string)x.Elements().FirstOrDefault(y =>
                                y.Name.LocalName == "Name"), required, StringComparison.OrdinalIgnoreCase)))
                            invalid.Add(type + " [" + controlName + "]." + required +
                                " is missing its metadata-initialized Boolean property");
                    }
                }
                foreach (var property in propertyElements)
                {
                    var propertyNameElement = property.Elements().FirstOrDefault(x => x.Name.LocalName == "Name");
                    var propertyName = propertyNameElement == null ? null : propertyNameElement.Value;
                    if (string.IsNullOrWhiteSpace(propertyName) ||
                        !declaredBooleanProperties.Contains(propertyName)) continue;
                    var values = new[] { "DisplayValue", "NameValue", "Value" }
                        .Select(elementName => new
                        {
                            Name = elementName,
                            Element = property.Elements().FirstOrDefault(x => x.Name.LocalName == elementName)
                        }).ToList();
                    bool parsed;
                    if (values.Any(x => x.Element == null || string.IsNullOrWhiteSpace(x.Element.Value) ||
                        !bool.TryParse(x.Element.Value, out parsed)))
                    {
                        invalid.Add(type + " [" + controlName + "]." + propertyName +
                            " has a missing, empty, or invalid Boolean DisplayValue/NameValue/Value triple");
                        continue;
                    }
                    var normalized = values.Select(x => bool.Parse(x.Element.Value)).Distinct().ToList();
                    if (normalized.Count != 1)
                        invalid.Add(type + " [" + controlName + "]." + propertyName +
                            " has inconsistent Boolean DisplayValue/NameValue/Value values");
                }
            }
            if (invalid.Count > 0)
                throw new CliException("K2 Form '" + name +
                    "' contains browser Designer-incompatible Boolean control properties: " +
                    string.Join("; ", invalid.ToArray()) +
                    ". The Authoring.Form XML round trip does not validate every property-editor representation.");
        }

        private static void VerifyDesignerAuthoringHydration(string definition, string name, bool isForm,
            IDictionary<string, string> formControlFlags = null,
            IDictionary<string, ISet<string>> formBooleanControlProperties = null,
            IDictionary<string, ISet<string>> formInitialBooleanControlProperties = null)
        {
            try
            {
                string roundTrip;
                if (isForm)
                {
                    VerifyFormControlAvailability(definition, name, formControlFlags);
                    VerifyFormBooleanControlProperties(definition, name, formBooleanControlProperties,
                        formInitialBooleanControlProperties);
                    var hydrated = new SourceCode.Forms.Authoring.Form();
                    hydrated.FromXml(definition);
                    roundTrip = hydrated.ToXml();
                }
                else
                {
                    var hydrated = new SourceCode.Forms.Authoring.View();
                    hydrated.FromXml(definition);
                    roundTrip = hydrated.ToXml();
                }
                if (string.IsNullOrWhiteSpace(roundTrip))
                    throw new CliException("K2 " + (isForm ? "Form" : "View") + " '" + name +
                        "' produced an empty definition after Designer authoring-model hydration.");
                Console.WriteLine("Designer authoring hydration: OK (" +
                    (isForm ? "Form" : "View") + " " + name + ")");
            }
            catch (CliException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CliException("K2 " + (isForm ? "Form" : "View") + " '" + name +
                    "' cannot hydrate through the installed Designer authoring model: " + ex.Message);
            }
        }

        private static string ReadSampleValue(System.Data.DataRow row, string property)
        {
            if (row == null || row.Table == null || !row.Table.Columns.Contains(property)) return null;
            var value = row[property];
            return value == null || value == DBNull.Value
                ? null
                : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        internal IEnumerable<KeyValuePair<ViewDefinition, FieldValidationDefinition>> PatternValidations(
            bool includeReusableViews = true)
        {
            return _manifest.Application.Views
                .Where(view => includeReusableViews || !view.ReuseExisting)
                .SelectMany(view => view.Validations
                .Where(validation => FieldValidationDefinitionXml.RequiresNativeValidationPattern(view, validation))
                .Select(validation => new KeyValuePair<ViewDefinition, FieldValidationDefinition>(view, validation)));
        }

        private void PrepareValidationPatterns()
        {
            foreach (var pair in PatternValidations())
            {
                pair.Value.ValidationPatternName = ValidationPatternName(pair.Key, pair.Value);
                pair.Value.ValidationPatternExpression = FieldValidationDefinitionXml.BuildPattern(pair.Value);
                if (pair.Value.ValidationPatternGuid == Guid.Empty)
                    pair.Value.ValidationPatternGuid = Guid.NewGuid();
            }
        }

        private void EnsureValidationPatterns(FormsManager manager, bool createOrUpdate)
        {
            var contracts = PatternValidations().ToList();
            if (contracts.Count == 0) return;
            var live = manager.GetValidationPatterns().ValidationPatterns.Cast<ManagementValidationPattern>().ToList();
            foreach (var pair in contracts)
            {
                var validation = pair.Value;
                var pattern = live.SingleOrDefault(x =>
                    string.Equals(x.Name, validation.ValidationPatternName, StringComparison.OrdinalIgnoreCase));
                if (pattern == null)
                {
                    if (!createOrUpdate)
                        throw new CliException("K2 validation pattern is missing for View '" + pair.Key.Name +
                            "' property '" + validation.Property + "': " + validation.ValidationPatternName);
                    pattern = manager.SetValidationPattern(new ManagementValidationPattern
                    {
                        Name = validation.ValidationPatternName,
                        Pattern = validation.ValidationPatternExpression,
                        Message = validation.Message,
                        Example = validation.Example ?? string.Empty,
                        Flags = 0
                    });
                    Console.WriteLine("Validation pattern: created (" + pattern.Name + ", " + pattern.Guid + ")");
                    live.Add(pattern);
                }
                else if (!string.Equals(pattern.Pattern, validation.ValidationPatternExpression, StringComparison.Ordinal) ||
                         !string.Equals(pattern.Message ?? string.Empty, validation.Message ?? string.Empty, StringComparison.Ordinal) ||
                         !string.Equals(pattern.Example ?? string.Empty, validation.Example ?? string.Empty, StringComparison.Ordinal))
                {
                    if (!createOrUpdate)
                        throw new CliException("K2 validation pattern differs from the manifest contract: " +
                            validation.ValidationPatternName);
                    pattern.Pattern = validation.ValidationPatternExpression;
                    pattern.Message = validation.Message;
                    pattern.Example = validation.Example ?? string.Empty;
                    manager.SetValidationPattern(pattern);
                    Console.WriteLine("Validation pattern: updated (" + pattern.Name + ", " + pattern.Guid + ")");
                }
                validation.ValidationPatternGuid = pattern.Guid;
            }
        }

        private void DeleteOwnedValidationPatterns(FormsManager manager)
        {
            var contracts = PatternValidations(false).ToList();
            if (contracts.Count == 0) return;
            var live = manager.GetValidationPatterns().ValidationPatterns.Cast<ManagementValidationPattern>().ToList();
            foreach (var pair in contracts)
            {
                var validation = pair.Value;
                validation.ValidationPatternName = ValidationPatternName(pair.Key, validation);
                validation.ValidationPatternExpression = FieldValidationDefinitionXml.BuildPattern(validation);
                var pattern = live.SingleOrDefault(x =>
                    string.Equals(x.Name, validation.ValidationPatternName, StringComparison.OrdinalIgnoreCase));
                if (pattern == null)
                {
                    Console.WriteLine("Validation pattern: already absent (" + validation.ValidationPatternName + ")");
                    continue;
                }
                if (!string.Equals(pattern.Pattern, validation.ValidationPatternExpression, StringComparison.Ordinal) ||
                    !string.Equals(pattern.Message ?? string.Empty, validation.Message ?? string.Empty, StringComparison.Ordinal))
                    throw new CliException("Refusing to delete validation pattern '" + pattern.Name +
                        "' because its definition no longer matches this manifest.");
                manager.DeleteValidationPattern(pattern.Name);
                Console.WriteLine("Validation pattern: deleted (" + pattern.Name + ", " + pattern.Guid + ")");
            }
        }

        private string ValidationPatternName(ViewDefinition view, FieldValidationDefinition validation)
        {
            var raw = "K2Skills." + _manifest.Name + "." + view.Name + "." + validation.Property;
            raw = new string(raw.Select(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_' ? ch : '_').ToArray());
            if (raw.Length <= 120) return raw;
            unchecked
            {
                uint hash = 2166136261;
                foreach (var ch in raw) hash = (hash ^ ch) * 16777619;
                return raw.Substring(0, 111) + "." + hash.ToString("x8");
            }
        }

        private static bool IsCurrentIdentity(string owner)
        {
            if (string.IsNullOrWhiteSpace(owner)) return true;
            var current = WindowsIdentity.GetCurrent().Name ?? string.Empty;
            Func<string, string> normalize = value => (value ?? string.Empty).Trim().Replace("K2:", string.Empty).Replace("K2\\", string.Empty);
            return string.Equals(normalize(owner), normalize(current), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOwnedOrOrphanedCategory(string actual, string expected, bool manifestOnly)
        {
            if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) return true;
            if (!manifestOnly || string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected)) return false;
            return expected.StartsWith(actual.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private void SmokeTestRuntime(string formName)
        {
            var url = _manifest.Verification.RuntimeBaseUrl.TrimEnd('/') + "/Runtime/Form/" + System.Web.HttpUtility.UrlEncode(formName) + "/";
            var stopwatch = Stopwatch.StartNew();
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UseDefaultCredentials = true;
            request.AllowAutoRedirect = false;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.UserAgent = "k2forms/0.17.0";
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    stopwatch.Stop();
                    var code = (int)response.StatusCode;
                    if (code >= 300 && code < 400)
                    {
                        var location = response.Headers[HttpResponseHeader.Location];
                        if (string.IsNullOrWhiteSpace(location)) throw new CliException("K2 runtime returned a redirect without a location for form " + formName + ".");
                        Console.WriteLine("Runtime route: reachable-authentication-required (" + formName + ", HTTP " + code + ", " + stopwatch.ElapsedMilliseconds + " ms; authenticated rendering and interaction not verified)");
                        return;
                    }
                    using (var stream = response.GetResponseStream())
                    {
                        if (stream != null)
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                var content = reader.ReadToEnd();
                                if (content.IndexOf("form could not be found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    content.IndexOf("resource cannot be found", StringComparison.OrdinalIgnoreCase) >= 0)
                                    throw new CliException("K2 runtime reported that form was not found: " + formName);
                            }
                        }
                    }
                    if (code < 200 || code >= 400)
                        throw new CliException("K2 runtime returned HTTP " + code + " for form " + formName + ".");
                    Console.WriteLine("Runtime render smoke test: OK (" + formName + ", HTTP " + code + ", " + stopwatch.ElapsedMilliseconds + " ms)");
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response == null) throw;
                using (response)
                {
                    throw new CliException("K2 runtime returned HTTP " + (int)response.StatusCode + " for form " + formName + ".");
                }
            }
        }

        private T WithFormsManager<T>(Func<FormsManager, T> action)
        {
            var manager = new FormsManager();
            try
            {
                manager.CreateConnection();
                manager.Connection.Open(BuildConnectionString());
                return action(manager);
            }
            finally
            {
                if (manager.Connection != null)
                {
                    manager.Connection.Close();
                    manager.DeleteConnection();
                }
                manager.Dispose();
            }
        }

        private StyleProfileInfo ResolveStyleProfile(FormsManager manager)
        {
            if (!_manifest.Application.Forms.Any(UsesStyleProfile)) return null;
            var value = _manifest.Application.StyleProfile;
            if (string.IsNullOrWhiteSpace(value)) return null;
            Guid guid;
            var profiles = manager.GetStyleProfiles().StyleProfiles.Cast<StyleProfileInfo>().Where(x =>
                (Guid.TryParse(value, out guid) && x.Guid == guid) ||
                string.Equals(x.Name, value, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.DisplayName, value, StringComparison.OrdinalIgnoreCase)).ToList();
            if (profiles.Count == 0) throw new CliException("K2 style profile not found: " + value + ". Available: " + string.Join(", ", manager.GetStyleProfiles().StyleProfiles.Cast<StyleProfileInfo>().Select(x => x.DisplayName + " [" + x.Name + "]").ToArray()));
            if (profiles.Count > 1) throw new CliException("K2 style profile is ambiguous; use its GUID: " + value);
            return profiles[0];
        }

        internal ResolvedCommonHeader ResolveCommonHeader(FormsManager manager)
        {
            if (!_manifest.Application.Forms.Any(UsesCommonHeader)) return null;
            var allowImplicitEnvironment = _manifest.Application.CommonHeader == null &&
                _manifest.Application.Forms.Any(x => x.UseCommonHeader == true);
            var configured = EnvironmentCommonHeader.ResolveDesired(_manifest.Application, allowImplicitEnvironment);
            if (configured == null)
                throw new CliException("At least one Form requests the common header, but no application or environment common-header contract is selected.");
            return ResolveCommonHeader(manager, configured);
        }

        private T WithCategoryServer<T>(Func<CategoryServer, T> action)
        {
            var server = new CategoryServer();
            try
            {
                server.CreateConnection();
                server.Connection.Open(BuildConnectionString());
                return action(server);
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

        private ResolvedCommonHeader ResolveCommonHeader(FormsManager manager, CommonHeaderDefinition configured)
        {
            ViewInfo info = null;
            if (configured.ViewGuid != Guid.Empty && manager.CheckViewExists(configured.ViewGuid)) info = manager.GetView(configured.ViewGuid);
            if (info == null && !string.IsNullOrWhiteSpace(configured.View) && manager.CheckViewExists(configured.View)) info = manager.GetView(configured.View);
            if (info == null) throw new CliException("Configured common header view is not installed: " + configured.View);
            if (configured.ViewGuid != Guid.Empty && info.Guid != configured.ViewGuid)
                throw new CliException("Configured common header view GUID does not match K2: " + configured.View + " (profile=" + configured.ViewGuid + ", K2=" + info.Guid + ")");
            if (_manifest.Application.Views.Any(x => string.Equals(x.Name, info.Name, StringComparison.OrdinalIgnoreCase)))
                throw new CliException("The common header is an external reused view and must not also be declared in application.views: " + info.Name);
            var availableParameters = info.Parameters.Cast<SourceCode.Forms.Management.ViewParameter>().Select(x => x.Name).ToList();
            foreach (var name in configured.Parameters.Keys)
                if (!availableParameters.Contains(name, StringComparer.OrdinalIgnoreCase))
                    throw new CliException("Configured common header parameter is not available on '" + info.Name + "': " + name);

            var initializeDefinitionId = Guid.Empty;
            var serverRules = new List<ResolvedHeaderRule>();
            XDocument viewDocument = null;
            var controlTransfers = new List<ResolvedHeaderControlTransfer>();
            if (configured.ServerLoadControlTransfers != null && configured.ServerLoadControlTransfers.Count > 0)
            {
                viewDocument = XDocument.Parse(manager.GetViewDefinition(info.Guid));
                foreach (var configuredTransfer in configured.ServerLoadControlTransfers)
                {
                    var control = viewDocument.Descendants().FirstOrDefault(x => x.Name.LocalName == "Control" && x.Attribute("ID") != null &&
                        (string.Equals((string)x.Element(x.Name.Namespace + "Name"), configuredTransfer.Key, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals((string)x.Element(x.Name.Namespace + "DisplayName"), configuredTransfer.Key, StringComparison.OrdinalIgnoreCase)));
                    Guid controlGuid;
                    if (control == null || !Guid.TryParse((string)control.Attribute("ID"), out controlGuid))
                        throw new CliException("Configured common header server-load transfer control is not available on '" + info.Name + "': " + configuredTransfer.Key);
                    controlTransfers.Add(new ResolvedHeaderControlTransfer
                    {
                        ControlGuid = controlGuid,
                        ControlName = (string)control.Element(control.Name.Namespace + "Name") ?? configuredTransfer.Key,
                        ValueTemplate = configuredTransfer.Value
                    });
                }
            }
            if (!string.IsNullOrWhiteSpace(configured.InitializeEvent))
            {
                viewDocument = XDocument.Parse(manager.GetViewDefinition(info.Guid));
                var events = viewDocument.Descendants().Where(x => x.Name.LocalName == "Event" &&
                    string.Equals((string)x.Attribute("SourceType"), "View", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Attribute("Type"), "User", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Element(x.Name.Namespace + "Name"), configured.InitializeEvent, StringComparison.OrdinalIgnoreCase)).ToList();
                var userEvent = events.FirstOrDefault();
                if (userEvent == null) throw new CliException("Configured common header user initialization rule is not available on '" + info.Name + "': " + configured.InitializeEvent);
                Guid.TryParse((string)userEvent.Attribute("DefinitionID"), out initializeDefinitionId);
            }
            foreach (var serverRuleName in configured.ServerRules ?? new List<string>())
            {
                if (viewDocument == null) viewDocument = XDocument.Parse(manager.GetViewDefinition(info.Guid));
                var rule = viewDocument.Descendants().FirstOrDefault(x => x.Name.LocalName == "Event" &&
                    string.Equals((string)x.Attribute("SourceType"), "View", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Attribute("Type"), "User", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Element(x.Name.Namespace + "Name"), serverRuleName, StringComparison.OrdinalIgnoreCase));
                if (rule == null) throw new CliException("Configured common header server rule is not available on '" + info.Name + "': " + serverRuleName);
                Guid definitionId;
                if (!Guid.TryParse((string)rule.Attribute("DefinitionID"), out definitionId))
                    throw new CliException("Configured common header server rule has an invalid definition ID: " + serverRuleName);
                serverRules.Add(new ResolvedHeaderRule { Name = serverRuleName, DefinitionId = definitionId });
            }
            ResolvedCommonFooter footer = null;
            if (configured.Footer != null)
            {
                ViewInfo footerInfo = null;
                if (configured.Footer.ViewGuid != Guid.Empty && manager.CheckViewExists(configured.Footer.ViewGuid)) footerInfo = manager.GetView(configured.Footer.ViewGuid);
                if (footerInfo == null && !string.IsNullOrWhiteSpace(configured.Footer.View) && manager.CheckViewExists(configured.Footer.View)) footerInfo = manager.GetView(configured.Footer.View);
                if (footerInfo == null) throw new CliException("Configured common footer view is not installed: " + configured.Footer.View);
                if (configured.Footer.ViewGuid != Guid.Empty && footerInfo.Guid != configured.Footer.ViewGuid)
                    throw new CliException("Configured common footer view GUID does not match K2: " + configured.Footer.View);
                if (_manifest.Application.Views.Any(x => string.Equals(x.Name, footerInfo.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new CliException("The common footer is an external reused view and must not also be declared in application.views: " + footerInfo.Name);
                footer = new ResolvedCommonFooter
                {
                    ViewGuid = footerInfo.Guid, ViewName = footerInfo.Name, DisplayName = footerInfo.DisplayName,
                    CategoryPath = footerInfo.CategoryPath, Title = configured.Footer.Title ?? string.Empty
                };
            }
            return new ResolvedCommonHeader
            {
                ViewGuid = info.Guid, ViewName = info.Name, DisplayName = info.DisplayName,
                CategoryPath = info.CategoryPath, Title = configured.Title ?? string.Empty,
                InstanceName = configured.InstanceName, IsCollapsible = configured.IsCollapsible,
                InitializeEvent = configured.InitializeEvent, InitializeEventDefinitionId = initializeDefinitionId,
                ServerRules = serverRules,
                ServerRulesBeforeControlTransfers = configured.ServerRulesBeforeControlTransfers,
                Parameters = configured.Parameters ?? new Dictionary<string, string>(),
                ServerLoadControlTransfers = controlTransfers,
                Footer = footer
            };
        }

        private ResolvedCommonHeader ResolveCommonHeaderRemovalCandidate(FormsManager manager)
        {
            var configured = EnvironmentCommonHeader.ResolveRemovalCandidate(_manifest.Application);
            if (configured == null) return null;
            if (configured.ViewGuid != Guid.Empty)
            {
                return new ResolvedCommonHeader
                {
                    ViewGuid = configured.ViewGuid,
                    ViewName = configured.View,
                    DisplayName = configured.View,
                    ServerRules = new List<ResolvedHeaderRule>(),
                    Parameters = new Dictionary<string, string>(),
                    ServerLoadControlTransfers = new List<ResolvedHeaderControlTransfer>(),
                    Footer = configured.Footer == null || configured.Footer.ViewGuid == Guid.Empty ? null :
                        new ResolvedCommonFooter
                        {
                            ViewGuid = configured.Footer.ViewGuid,
                            ViewName = configured.Footer.View,
                            DisplayName = configured.Footer.View
                        }
                };
            }
            return ResolveCommonHeader(manager, configured);
        }

        private bool UsesStyleProfile(FormDefinition form)
        {
            return FormFrameworkUsage.UsesStyleProfile(_manifest.Application, form);
        }

        private bool UsesCommonHeader(FormDefinition form)
        {
            return FormFrameworkUsage.UsesCommonHeader(_manifest.Application, form);
        }

        private ResolvedCommonHeader SelectCommonHeader(FormDefinition form, ResolvedCommonHeader available)
        {
            if (!UsesCommonHeader(form)) return null;
            if (available == null)
                throw new CliException("Form '" + form.Name + "' requests a common header, but no resolved common-header contract is available.");
            var useFooter = FormFrameworkUsage.UsesCommonFooter(_manifest.Application, form, available.Footer != null);
            if (useFooter && available.Footer == null)
                throw new CliException("Form '" + form.Name + "' requests a common footer, but the selected common-header contract has no footer.");
            return new ResolvedCommonHeader
            {
                ViewGuid = available.ViewGuid,
                ViewName = available.ViewName,
                DisplayName = available.DisplayName,
                CategoryPath = available.CategoryPath,
                Title = available.Title,
                InstanceName = available.InstanceName,
                IsCollapsible = available.IsCollapsible,
                InitializeEvent = available.InitializeEvent,
                InitializeEventDefinitionId = available.InitializeEventDefinitionId,
                ServerRules = available.ServerRules,
                ServerRulesBeforeControlTransfers = available.ServerRulesBeforeControlTransfers,
                Parameters = available.Parameters,
                ServerLoadControlTransfers = available.ServerLoadControlTransfers,
                Footer = useFooter ? available.Footer : null
            };
        }

        private static IEnumerable<Guid> RedundantFrameworkGuids(ResolvedCommonHeader desired, ResolvedCommonHeader removable)
        {
            if (removable == null) return Enumerable.Empty<Guid>();
            var result = new List<Guid>();
            if (desired == null)
            {
                result.Add(removable.ViewGuid);
                if (removable.Footer != null) result.Add(removable.Footer.ViewGuid);
            }
            else if (desired.Footer == null && removable.Footer != null)
                result.Add(removable.Footer.ViewGuid);
            return result.Where(x => x != Guid.Empty).Distinct();
        }

        private Dictionary<string, string> ResolveHeaderParameters(ResolvedCommonHeader header, FormDefinition form)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (header == null) return result;
            var solutionCode = _manifest.Application.SolutionCode;
            if (string.IsNullOrWhiteSpace(solutionCode))
            {
                var separator = form.Name.IndexOf('.');
                solutionCode = separator > 0 ? form.Name.Substring(0, separator) : form.Name;
            }
            foreach (var parameter in header.Parameters)
            {
                result[parameter.Key] = ResolveHeaderTemplate(parameter.Value, form, solutionCode);
            }
            return result;
        }

        private Dictionary<Guid, ResolvedHeaderControlTransfer> ResolveHeaderControlTransfers(ResolvedCommonHeader header, FormDefinition form)
        {
            var result = new Dictionary<Guid, ResolvedHeaderControlTransfer>();
            if (header == null) return result;
            var solutionCode = _manifest.Application.SolutionCode;
            if (string.IsNullOrWhiteSpace(solutionCode))
            {
                var separator = form.Name.IndexOf('.');
                solutionCode = separator > 0 ? form.Name.Substring(0, separator) : form.Name;
            }
            foreach (var transfer in header.ServerLoadControlTransfers ?? new List<ResolvedHeaderControlTransfer>())
                result[transfer.ControlGuid] = new ResolvedHeaderControlTransfer
                {
                    ControlGuid = transfer.ControlGuid,
                    ControlName = transfer.ControlName,
                    ValueTemplate = ResolveHeaderTemplate(transfer.ValueTemplate, form, solutionCode)
                };
            return result;
        }

        private string ResolveHeaderTemplate(string template, FormDefinition form, string solutionCode)
        {
            return (template ?? string.Empty).Replace("{{form.name}}", form.Name)
                .Replace("{{application.name}}", _manifest.Name)
                .Replace("{{application.rootCategoryPath}}", _manifest.Application.RootCategoryPath)
                .Replace("{{solution.code}}", solutionCode);
        }

        private T WithSmartObjectServer<T>(Func<SmartObjectClientServer, T> action)
        {
            var server = new SmartObjectClientServer();
            try
            {
                server.CreateConnection();
                server.Connection.Open(BuildConnectionString());
                return action(server);
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

        private string BuildConnectionString()
        {
            var builder = new SCConnectionStringBuilder
            {
                Authenticate = true,
                Host = _manifest.K2.Host,
                Port = (uint)_manifest.K2.Port,
                Integrated = _manifest.K2.Integrated,
                IsPrimaryLogin = true,
                SecurityLabelName = _manifest.K2.SecurityLabel
            };
            if (!_manifest.K2.Integrated)
            {
                builder.WindowsDomain = _manifest.K2.Domain;
                builder.UserID = _manifest.K2.UserName;
                builder.Password = ReadRequiredEnvironmentVariable(_manifest.K2.PasswordEnvironmentVariable);
                builder.CachePassword = false;
            }
            return builder.ConnectionString;
        }

        private static AuthoringViewType ParseViewType(string value)
        {
            switch (value)
            {
                case "capture": return AuthoringViewType.Capture;
                case "list": return AuthoringViewType.List;
                case "content": return AuthoringViewType.Content;
                case "capture-list": return AuthoringViewType.List;
                default: throw new CliException("Unsupported view type: " + value);
            }
        }

        private static ViewCreationOption ParseViewOptions(IEnumerable<string> values)
        {
            var result = ViewCreationOption.None;
            foreach (var value in values)
            {
                switch (value.ToLowerInvariant())
                {
                    case "display-controls": result |= ViewCreationOption.FormDisplayControls; break;
                    case "all-properties": result |= ViewCreationOption.UseAllProperties; break;
                    case "all-methods": result |= ViewCreationOption.UseAllInstanceMethods; break;
                    case "labels-left": result |= ViewCreationOption.LabelsToLeftOfControls; break;
                    case "colon-labels": result |= ViewCreationOption.AddColonSuffixToLabels; break;
                    case "toolbar": result |= ViewCreationOption.CreateToolbar; break;
                    case "editable": result |= ViewCreationOption.IsEditable; break;
                }
            }
            return result;
        }

        private static FormGenerationOption ParseFormOptions(IEnumerable<string> values)
        {
            var result = FormGenerationOption.None;
            foreach (var value in values)
                if (value.Equals("no-tabs", StringComparison.OrdinalIgnoreCase)) result |= FormGenerationOption.NoTabs;
            return result;
        }

        private static FormBehaviorOption ParseFormBehaviors(IEnumerable<string> values)
        {
            var result = FormBehaviorOption.None;
            foreach (var value in values)
            {
                switch (value.ToLowerInvariant())
                {
                    case "load-form-list-click": result |= FormBehaviorOption.LoadFormListClick; break;
                    case "refresh-list-form-submit": result |= FormBehaviorOption.RefreshListFormSubmit; break;
                    case "refresh-list-form-load": result |= FormBehaviorOption.RefreshListFormLoad; break;
                }
            }
            return result;
        }

        private static string ReadRequiredEnvironmentVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value)) throw new CliException("Required environment variable is not set: " + name);
            return value;
        }
    }

    internal sealed class ArtifactState
    {
        public string Kind { get; set; }
        public string Name { get; set; }
        public bool Exists { get; set; }
        public Guid Guid { get; set; }
        public string CategoryPath { get; set; }
        public int Version { get; set; }
        public bool CheckedOut { get; set; }
        public string Type { get; set; }
        public bool? UseLegacyTheme { get; set; }
        public string StyleProfile { get; set; }

        public static ArtifactState Absent(string kind, string name)
        {
            return new ArtifactState { Kind = kind, Name = name, Exists = false };
        }
    }
}
