using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Xml.Linq;
using SourceCode.Forms.Authoring;
using SourceCode.Forms.Management;
using SourceCode.Forms.Utilities;
using SourceCode.Hosting.Client.BaseAPI;
using SourceCode.SmartObjects.Client;
using AuthoringViewType = SourceCode.Forms.Authoring.ViewType;

namespace K2SmartFormsCli
{
    internal sealed class SmartFormsManager
    {
        private readonly SmartFormsManifest _manifest;

        public SmartFormsManager(SmartFormsManifest manifest)
        {
            _manifest = manifest;
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
                Console.WriteLine("K2 SmartForms connection: OK (" + elapsed.TotalMilliseconds.ToString("0") + " ms, theme " + _manifest.Application.Theme + ", styleProfile=" + (styleProfile == null ? "none" : styleProfile.DisplayName + " [" + styleProfile.Name + "]") + ")");
                Console.WriteLine("K2 common framework input: " + (commonHeader == null ? "none" : commonHeader.DisplayName + " [" + commonHeader.ViewName + "] from " + commonHeader.CategoryPath + "; footer=" + (commonHeader.Footer == null ? "none" : commonHeader.Footer.ViewName) + "; server-load transfers=" + commonHeader.ServerLoadControlTransfers.Count));
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
                    Console.WriteLine("Lookup source: OK (" + source.Name + " <= " + smartObject.Name + "." + method.Name + ", value=" + valueProperty.Name + ", display=" + displayProperty.Name + ")");
                }
                return result;
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

        public void Deploy(bool resume, bool formsOnly)
        {
            CheckConnectionAndInputs();
            var lookupSources = LoadLookupRuntimeSources();
            var states = GetArtifactStates();
            var selectedStates = formsOnly ? states.Where(x => string.Equals(x.Kind, "Form", StringComparison.OrdinalIgnoreCase)).ToList() : states;
            var existing = selectedStates.Where(x => x.Exists).ToList();
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
                    using (var renderer = new AutoGenerator(manager.Connection))
                    {
                        foreach (var view in _manifest.Application.Views)
                        {
                            if (resume && manager.CheckViewExists(view.Name)) continue;
                            var viewGenerator = new ViewGenerator(ParseViewType(view.Type), ParseViewOptions(view.Options));
                            if (view.Type == "capture" || view.Type == "capture-list") viewGenerator.InputProperties.AddRange(view.Properties);
                            else viewGenerator.DisplayProperties.AddRange(view.Properties);
                            viewGenerator.InstanceMethods.AddRange(view.Methods);
                            if (!string.IsNullOrWhiteSpace(view.DefaultListMethod)) viewGenerator.DefaultListMethod = view.DefaultListMethod;
                            var generated = renderer.Generate(viewGenerator, view.SmartObject, view.Name);
                            var definition = ViewLookupDefinition.Apply(generated.ToXml(), view, lookupSources);
                            var isMaster = _manifest.Application.Forms.Any(f => f.MasterDetail != null && string.Equals(f.MasterDetail.MasterView, view.Name, StringComparison.OrdinalIgnoreCase));
                            var detailRelationships = _manifest.Application.Forms.Where(f => f.MasterDetail != null)
                                .SelectMany(f => f.MasterDetail.Details).Where(d => string.Equals(d.View, view.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                            var isDetail = detailRelationships.Count > 0;
                            definition = ViewPresentationDefinition.Apply(definition, view, isMaster, isDetail);
                            if (isDetail) definition = MasterDetailRules.SuppressUnfilteredDetailLoads(definition, view.Name, detailRelationships);
                            renderedViews[view.Name] = definition;
                        }
                    }
                    Console.WriteLine("Pre-render validation: " + renderedViews.Count + " View definition(s) generated before K2 mutation.");
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
                        if (!manager.CheckViewExists(view.Name)) continue;
                        var info = manager.GetView(view.Name);
                        manager.DeleteView(info.Guid);
                        Console.WriteLine("View: removed for replacement (" + view.Name + ", " + info.Guid + ")");
                    }
                }

                using (var generator = new AutoGenerator(manager.Connection))
                {
                    foreach (var view in _manifest.Application.Views)
                    {
                        if (formsOnly) break;
                        if (resume && manager.CheckViewExists(view.Name))
                        {
                            var existingView = manager.GetView(view.Name);
                            Console.WriteLine("View: resumed existing (" + view.Name + ", " + existingView.Guid + ", v" + existingView.Version + ")");
                            continue;
                        }
                        var definition = renderedViews[view.Name];
                        manager.DeployViews(definition, _manifest.Application.GetViewCategoryPath(view), _manifest.Application.CheckIn);
                        var info = manager.GetView(view.Name);
                        Console.WriteLine("View: deployed (" + view.Name + ", " + info.Guid + ", " + info.Type + ", category " + info.CategoryPath + ", " + view.LookupControls.Count + " lookup control(s))");
                    }

                    foreach (var form in _manifest.Application.Forms)
                    {
                        if (resume && manager.CheckFormExists(form.Name))
                        {
                            var existingForm = manager.GetForm(form.Name);
                            Console.WriteLine("Form: resumed existing (" + form.Name + ", " + existingForm.Guid + ", v" + existingForm.Version + ")");
                            continue;
                        }
                        var formGenerator = new FormGenerator(ParseFormOptions(form.Options), ParseFormBehaviors(form.Behaviors), _manifest.Application.Theme);
                        var formViews = commonHeader == null ? form.Views.ToArray() :
                            new[] { commonHeader.ViewName }.Concat(form.Views).Concat(commonHeader.Footer == null ? new string[0] : new[] { commonHeader.Footer.ViewName }).ToArray();
                        var generated = generator.Generate(formGenerator, formViews, form.Name);
                        var definition = FormThemeDefinition.SetUseLegacyTheme(generated.ToXml(), form.UseLegacyTheme);
                        if (styleProfile != null) definition = FormThemeDefinition.SetStyleProfile(definition, styleProfile.Guid, styleProfile.Name);
                        definition = FormLayoutDefinition.Apply(definition, form, commonHeader, ResolveHeaderParameters(commonHeader, form), ResolveHeaderControlTransfers(commonHeader, form));
                        var masterDetail = ResolvedMasterDetailRules.Resolve(manager, form);
                        definition = MasterDetailRules.Apply(definition, form, masterDetail);
                        manager.DeployForms(definition, _manifest.Application.GetFormCategoryPath(form), _manifest.Application.CheckIn);
                        var info = manager.GetForm(form.Name);
                        Console.WriteLine("Form: deployed (" + form.Name + ", " + info.Guid + ", theme " + info.Theme.Name + ", styleProfile=" + (styleProfile == null ? "none" : styleProfile.Name) + ", legacyTheme=" + form.UseLegacyTheme.ToString().ToLowerInvariant() + ", commonHeader=" + (commonHeader == null ? "none" : commonHeader.ViewName) + ", commonFooter=" + (commonHeader == null || commonHeader.Footer == null ? "none" : commonHeader.Footer.ViewName) + ", tabs=" + form.Tabs.Count + ", worklist=" + form.Tabs.Any(x => x.Worklist != null).ToString().ToLowerInvariant() + ")");
                    }
                }
                return 0;
            });
        }

        public void Verify()
        {
            var runtimeForms = new List<string>();
            var lookupSources = LoadLookupRuntimeSources();
            WithFormsManager(delegate(FormsManager manager)
            {
                var expectedStyleProfile = ResolveStyleProfile(manager);
                var commonHeader = ResolveCommonHeader(manager);
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
                    var isMaster = _manifest.Application.Forms.Any(f => f.MasterDetail != null && string.Equals(f.MasterDetail.MasterView, declaredView.Name, StringComparison.OrdinalIgnoreCase));
                    var detailRelationships = _manifest.Application.Forms.Where(f => f.MasterDetail != null)
                        .SelectMany(f => f.MasterDetail.Details).Where(d => string.Equals(d.View, declaredView.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                    var isDetail = detailRelationships.Count > 0;
                    ViewPresentationDefinition.Verify(definition, declaredView, isMaster, isDetail);
                    if (isDetail) MasterDetailRules.VerifyDetailViewLoads(definition, declaredView.Name, detailRelationships);
                    Console.WriteLine("View verification: OK (" + expected + ", " + info.Guid + ", v" + info.Version + ", " + info.Type + ")");
                }

                foreach (var expected in _manifest.Verification.ExpectedForms)
                {
                    if (!manager.CheckFormExists(expected)) throw new CliException("Expected K2 Form is missing: " + expected);
                    var info = manager.GetForm(expected);
                    var definition = manager.GetFormDefinition(info.Guid);
                    if (string.IsNullOrWhiteSpace(definition)) throw new CliException("K2 Form has an empty definition: " + expected);
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
                    if (expectedStyleProfile == null && actualStyleProfile != null)
                        throw new CliException("K2 Form has style profile '" + actualStyleProfile.Name + "' but the manifest expects none: " + expected);
                    if (expectedStyleProfile != null && (actualStyleProfile == null || actualStyleProfile.Guid != expectedStyleProfile.Guid))
                        throw new CliException("K2 Form style profile does not match '" + expectedStyleProfile.DisplayName + "' [" + expectedStyleProfile.Name + "]: " + expected);
                    FormLayoutDefinition.Verify(definition, declaredForm, commonHeader, ResolveHeaderParameters(commonHeader, declaredForm), ResolveHeaderControlTransfers(commonHeader, declaredForm));
                    MasterDetailRules.Verify(definition, declaredForm, ResolvedMasterDetailRules.Resolve(manager, declaredForm));
                    foreach (var viewName in declaredForm.Views)
                    {
                        var viewGuid = manager.GetView(viewName).Guid.ToString();
                        if (definition.IndexOf(viewGuid, StringComparison.OrdinalIgnoreCase) < 0)
                            throw new CliException("K2 Form '" + expected + "' does not reference expected view '" + viewName + "'.");
                    }
                    if (commonHeader != null && definition.IndexOf(commonHeader.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase) < 0)
                        throw new CliException("K2 Form '" + expected + "' does not reference environment common header '" + commonHeader.ViewName + "'.");
                    if (commonHeader != null && commonHeader.Footer != null && definition.IndexOf(commonHeader.Footer.ViewGuid.ToString(), StringComparison.OrdinalIgnoreCase) < 0)
                        throw new CliException("K2 Form '" + expected + "' does not reference environment common footer '" + commonHeader.Footer.ViewName + "'.");
                    Console.WriteLine("Form verification: OK (" + expected + ", " + info.Guid + ", v" + info.Version + ", theme " + info.Theme.Name + ", styleProfile=" + (actualStyleProfile == null ? "none" : actualStyleProfile.Name) + ", legacyTheme=" + useLegacyTheme.Value.ToString().ToLowerInvariant() + ")");
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

        public void Cleanup(bool manifestOnly)
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
                return 0;
            });
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
            request.UserAgent = "k2forms/0.15.2";
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
            var configured = EnvironmentCommonHeader.Resolve(_manifest.Application);
            if (configured == null) return null;
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
