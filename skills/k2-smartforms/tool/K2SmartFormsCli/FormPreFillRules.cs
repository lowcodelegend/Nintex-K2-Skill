using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SourceCode.Forms.Management;

namespace K2SmartFormsCli
{
    internal sealed class ResolvedFormPreFill
    {
        public List<ResolvedPreFillTarget> Targets { get; private set; }
        public List<string> ManualProperties { get; private set; }

        public ResolvedFormPreFill()
        {
            Targets = new List<ResolvedPreFillTarget>();
            ManualProperties = new List<string>();
        }

        public static ResolvedFormPreFill Resolve(FormsManager manager, FormDefinition form,
            IEnumerable<ViewDefinition> declaredViews, IDictionary<string, LookupRuntimeSource> lookupSources)
        {
            var result = new ResolvedFormPreFill();
            if (!form.PreFill.EffectiveEnabled) return result;
            foreach (var view in declaredViews.Where(x =>
                form.Views.Contains(x.Name, StringComparer.OrdinalIgnoreCase) &&
                new[] { "capture", "capture-list" }.Contains(x.Type, StringComparer.OrdinalIgnoreCase)))
            {
                var info = manager.GetView(view.Name);
                var document = XDocument.Parse(manager.GetViewDefinition(info.Guid));
                foreach (var property in view.Properties)
                {
                    if (view.HiddenProperties.Contains(property, StringComparer.OrdinalIgnoreCase) ||
                        view.ReadOnlyProperties.Contains(property, StringComparer.OrdinalIgnoreCase) ||
                        view.DefaultValues.Keys.Contains(property, StringComparer.OrdinalIgnoreCase))
                        continue;
                    XElement control;
                    try { control = ViewPresentationDefinition.FindEditableFieldControl(document, view, property); }
                    catch (CliException)
                    {
                        result.ManualProperties.Add(view.Name + "." + property);
                        continue;
                    }
                    if (IsUnavailable(control)) continue;
                    var validation = view.Validations.SingleOrDefault(x =>
                        string.Equals(x.Property, property, StringComparison.OrdinalIgnoreCase));
                    var binding = view.LookupControls.SingleOrDefault(x =>
                        string.Equals(x.Property, property, StringComparison.OrdinalIgnoreCase));
                    string value;
                    if (binding != null)
                    {
                        LookupRuntimeSource source;
                        if (binding.Cascade != null ||
                            !lookupSources.TryGetValue(binding.Lookup, out source) ||
                            string.IsNullOrWhiteSpace(source.SampleValue))
                        {
                            result.ManualProperties.Add(view.Name + "." + property);
                            continue;
                        }
                        value = source.SampleValue;
                        if (!FieldValidationDefinitionXml.SatisfiesTextConstraint(value, validation))
                        {
                            result.ManualProperties.Add(view.Name + "." + property);
                            continue;
                        }
                    }
                    else if (new[] { "DropDown", "Picker", "RadioButtonList" }.Contains(
                        (string)control.Attribute("Type"), StringComparer.OrdinalIgnoreCase))
                    {
                        result.ManualProperties.Add(view.Name + "." + property);
                        continue;
                    }
                    else if (!TryBuildValue(control, property, validation, out value))
                    {
                        result.ManualProperties.Add(view.Name + "." + property);
                        continue;
                    }
                    result.Targets.Add(new ResolvedPreFillTarget
                    {
                        ViewGuid = info.Guid,
                        ViewName = view.Name,
                        Property = property,
                        ControlId = (string)control.Attribute("ID"),
                        ControlName = ChildValue(control, "Name") ?? property,
                        Value = value
                    });
                }
            }
            result.ManualProperties = result.ManualProperties
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            return result;
        }

        private static bool IsUnavailable(XElement control)
        {
            return IsTrue(PropertyValue(control, "IsReadOnly")) ||
                   IsFalse(PropertyValue(control, "IsEnabled")) ||
                   IsFalse(PropertyValue(control, "IsVisible"));
        }

        internal static bool TryBuildValue(XElement control, string property,
            FieldValidationDefinition validation, out string value)
        {
            value = null;
            var type = ((string)control.Attribute("Type") ?? string.Empty).Trim();
            var dataType = (PropertyValue(control, "DataType") ?? string.Empty).Trim();
            if (new[] { "File", "FilePostBack", "Image", "Signature" }
                .Contains(type, StringComparer.OrdinalIgnoreCase) ||
                string.Equals(dataType, "File", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(type, "CheckBox", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dataType, "Boolean", StringComparison.OrdinalIgnoreCase))
            {
                value = "true";
                return true;
            }
            if (new[] { "Number", "Decimal", "AutoNumber" }.Contains(dataType, StringComparer.OrdinalIgnoreCase))
            {
                value = BuildNumber(validation).ToString(CultureInfo.InvariantCulture);
                return true;
            }
            if (new[] { "Date", "DateTime" }.Contains(dataType, StringComparer.OrdinalIgnoreCase) ||
                string.Equals(type, "Calendar", StringComparison.OrdinalIgnoreCase))
            {
                value = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return true;
            }
            if (string.Equals(dataType, "Time", StringComparison.OrdinalIgnoreCase))
            {
                value = "12:00:00";
                return true;
            }
            if (new[] { "Guid", "AutoGuid" }.Contains(dataType, StringComparer.OrdinalIgnoreCase))
            {
                value = Guid.NewGuid().ToString();
                return true;
            }
            if (!new[] { "TextBox", "TextArea", "DropDown", "Picker", "RadioButtonList" }
                .Contains(type, StringComparer.OrdinalIgnoreCase) &&
                !new[] { "Text", "Memo", "HyperLink" }.Contains(dataType, StringComparer.OrdinalIgnoreCase))
                return false;
            return TryBuildText(property, validation, out value);
        }

        private static decimal BuildNumber(FieldValidationDefinition validation)
        {
            if (validation == null || (!validation.Minimum.HasValue && !validation.Maximum.HasValue)) return 1m;
            if (validation.Minimum.HasValue && validation.Maximum.HasValue)
            {
                if (validation.Minimum.Value == validation.Maximum.Value) return validation.Minimum.Value;
                return validation.Minimum.Value + ((validation.Maximum.Value - validation.Minimum.Value) / 2m);
            }
            if (validation.Minimum.HasValue)
                return validation.Minimum.Value + (validation.ExclusiveMinimum ? 1m : 0m);
            return validation.Maximum.Value - (validation.ExclusiveMaximum ? 1m : 0m);
        }

        private static bool TryBuildText(string property, FieldValidationDefinition validation, out string value)
        {
            value = null;
            if (validation != null && !string.IsNullOrWhiteSpace(validation.Pattern))
            {
                if (string.IsNullOrWhiteSpace(validation.Example)) return false;
                return TryValidatedText(validation.Example, validation, out value);
            }

            var format = validation == null ? string.Empty : validation.Format ?? string.Empty;
            var minimum = validation != null && validation.MinLength.HasValue ? validation.MinLength.Value : 0;
            var maximum = validation != null ? validation.MaxLength : null;
            switch (format.ToLowerInvariant())
            {
                case "email":
                    const string emailSuffix = "@b.co";
                    var localLength = Math.Max(1, minimum - emailSuffix.Length);
                    if (maximum.HasValue && localLength + emailSuffix.Length > maximum.Value) return false;
                    value = new string('x', localLength) + emailSuffix;
                    break;
                case "phone":
                    var phoneLength = Math.Max(7, minimum);
                    if (phoneLength > 25 || (maximum.HasValue && phoneLength > maximum.Value)) return false;
                    value = "+" + new string('1', phoneLength - 1);
                    break;
                case "url":
                    const string urlPrefix = "https://x.co";
                    var urlLength = Math.Max(urlPrefix.Length, minimum);
                    if (maximum.HasValue && urlLength > maximum.Value) return false;
                    value = urlPrefix + new string('x', urlLength - urlPrefix.Length);
                    break;
                case "guid":
                    if (maximum.HasValue && maximum.Value < 36) return false;
                    value = Guid.NewGuid().ToString();
                    break;
                default:
                    var semantic = SemanticDummy(property);
                    if (semantic != null && TryValidatedText(semantic, validation, out value)) return true;
                    value = BuildBoundedText(property, minimum, maximum);
                    break;
            }
            return TryValidatedText(value, validation, out value);
        }

        private static bool TryValidatedText(string candidate, FieldValidationDefinition validation, out string value)
        {
            value = candidate ?? string.Empty;
            if (validation == null) return true;
            if (validation.MaxLength.HasValue && value.Length > validation.MaxLength.Value)
                return false;
            if (validation.MinLength.HasValue && value.Length < validation.MinLength.Value)
                return false;
            if (FieldValidationDefinitionXml.RequiresValidationPattern(validation) &&
                !Regex.IsMatch(value, FieldValidationDefinitionXml.BuildPattern(validation), RegexOptions.ECMAScript))
                return false;
            return true;
        }

        private static string SemanticDummy(string property)
        {
            if (string.IsNullOrWhiteSpace(property)) return null;
            if (property.EndsWith("CountryCode", StringComparison.OrdinalIgnoreCase)) return "AE";
            if (property.EndsWith("CurrencyCode", StringComparison.OrdinalIgnoreCase)) return "AED";
            if (property.EndsWith("LanguageCode", StringComparison.OrdinalIgnoreCase)) return "en";
            return null;
        }

        private static string BuildBoundedText(string property, int minimum, int? maximum)
        {
            var candidate = HumanDummy(property);
            if (candidate.Length < minimum) candidate = candidate.PadRight(minimum, 'x');
            if (!maximum.HasValue || candidate.Length <= maximum.Value) return candidate;

            // Generate a deliberate boundary value. Never clip a descriptive label into
            // accidental data such as "Te" for a two-character code.
            var length = minimum > 0 ? minimum : maximum.Value;
            return new string('X', length);
        }

        private static string HumanDummy(string property)
        {
            var text = Regex.Replace(property ?? "Value", "([a-z0-9])([A-Z])", "$1 $2");
            return "Test " + text;
        }

        private static string PropertyValue(XElement control, string name)
        {
            var property = control.Descendants().FirstOrDefault(x => x.Name.LocalName == "Property" &&
                string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            return property == null ? null : ChildValue(property, "Value");
        }

        private static bool IsTrue(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFalse(string value)
        {
            return string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
        }

        private static string ChildValue(XElement parent, string name)
        {
            var child = parent.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            return child == null ? null : child.Value;
        }
    }

    internal sealed class ResolvedPreFillTarget
    {
        public Guid ViewGuid { get; set; }
        public string ViewName { get; set; }
        public string Property { get; set; }
        public string ControlId { get; set; }
        public string ControlName { get; set; }
        public string Value { get; set; }
    }

    internal static class FormPreFillRules
    {
        internal const string ButtonName = "btnPreFill";
        internal const string ButtonText = "Pre-fill";

        public static string Apply(string xml, FormDefinition definition, ResolvedFormPreFill resolved)
        {
            if (!definition.PreFill.EffectiveEnabled) return xml;
            var document = XDocument.Parse(xml);
            var form = document.Descendants().Single(x => x.Name.LocalName == "Form");
            var controls = RequiredChild(form, "Controls");
            if (controls.Elements().Any(x => x.Name.LocalName == "Control" &&
                string.Equals(ChildValue(x, "Name"), ButtonName, StringComparison.OrdinalIgnoreCase)))
                throw new CliException("Generated Form '" + definition.Name + "' already contains reserved control '" + ButtonName + "'.");

            var ns = form.Name.Namespace;
            var tableId = NewId(); var rowId = NewId(); var cellId = NewId(); var buttonId = NewId();
            var areaId = NewId(); var itemId = NewId();
            controls.Add(Control(ns, tableId, "Table", "tblPreFillActions",
                Property(ns, "IsResponsive", "true", "true", "true")));
            controls.Add(Control(ns, rowId, "Row", "Pre-fill Row"));
            controls.Add(Control(ns, cellId, "Cell", "Pre-fill Cell"));
            controls.Add(Control(ns, buttonId, "Button", ButtonName,
                Property(ns, "Text", ButtonText, ButtonText, ButtonText)));
            controls.Add(Control(ns, areaId, "Area", "Pre-fill Area"));
            controls.Add(Control(ns, itemId, "AreaItem", "Pre-fill Area Item"));

            var area = new XElement(ns + "Area", new XAttribute("ID", areaId),
                new XElement(ns + "Items", new XElement(ns + "Item", new XAttribute("ID", itemId),
                    new XElement(ns + "Canvas",
                        new XElement(ns + "Control", new XAttribute("ID", tableId), new XAttribute("LayoutType", "Grid"),
                            new XElement(ns + "Columns", new XElement(ns + "Column",
                                new XAttribute("ID", NewId()), new XAttribute("Size", "100%"))),
                            new XElement(ns + "Rows", new XElement(ns + "Row", new XAttribute("ID", rowId),
                                new XElement(ns + "Cells", new XElement(ns + "Cell", new XAttribute("ID", cellId),
                                    new XElement(ns + "Control", new XAttribute("ID", buttonId)))))))))));
            RequiredChild(SelectTargetPanel(form, definition), "Areas").Add(area);
            AddRule(form, definition, resolved, buttonId);
            var result = document.ToString(SaveOptions.DisableFormatting);
            Verify(result, definition, resolved);
            return result;
        }

        public static void Verify(string xml, FormDefinition definition, ResolvedFormPreFill resolved)
        {
            var document = XDocument.Parse(xml);
            var form = document.Descendants().Single(x => x.Name.LocalName == "Form");
            var controls = RequiredChild(form, "Controls");
            var buttons = controls.Elements().Where(x => x.Name.LocalName == "Control" &&
                string.Equals(ChildValue(x, "Name"), ButtonName, StringComparison.OrdinalIgnoreCase)).ToList();
            var namedEvents = form.Descendants().Where(x => x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("SourceName"), ButtonName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!definition.PreFill.EffectiveEnabled)
            {
                if (buttons.Count > 0 || namedEvents.Count > 0)
                    throw new CliException("K2 Form '" + definition.Name +
                        "' retains the test-only Pre-fill helper although preFill.enabled is false.");
                return;
            }
            if (buttons.Count != 1)
                throw new CliException("K2 Form '" + definition.Name + "' must contain exactly one test-only Pre-fill button.");
            var button = buttons[0];
            if (!string.Equals((string)button.Attribute("Type"), "Button", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ReadProperty(button, "Text"), ButtonText, StringComparison.Ordinal))
                throw new CliException("K2 Form '" + definition.Name + "' test-only Pre-fill button is malformed.");
            var buttonId = (string)button.Attribute("ID");
            var targetPanel = SelectTargetPanel(form, definition);
            var areas = RequiredChild(targetPanel, "Areas").Elements().Where(x => x.Name.LocalName == "Area").ToList();
            if (areas.Count == 0 || !areas[areas.Count - 1].Descendants().Any(x => x.Name.LocalName == "Control" &&
                string.Equals((string)x.Attribute("ID"), buttonId, StringComparison.OrdinalIgnoreCase)))
                throw new CliException("K2 Form '" + definition.Name +
                    "' test-only Pre-fill button is not at the bottom of the " +
                    (definition.GuidedJourney == null ? "last visible panel." : "first guided-journey panel."));

            var baseState = RequiredChild(RequiredChild(form, "States"), "State");
            ControlRuleDefinition.VerifySystemEvent(baseState, buttonId, "OnClick",
                "K2 Form '" + definition.Name + "' test-only Pre-fill button");
            var clicks = baseState.Descendants().Where(x => x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("Type"), "User", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)x.Attribute("SourceID"), buttonId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ChildValue(x, "Name"), "OnClick", StringComparison.OrdinalIgnoreCase)).ToList();
            if (clicks.Count != 1)
                throw new CliException("K2 Form '" + definition.Name +
                    "' test-only Pre-fill button must have exactly one OnClick rule.");
            var actions = clicks[0].Descendants().Where(x => x.Name.LocalName == "Action").ToList();
            var transfers = actions.Where(x => string.Equals((string)x.Attribute("Type"), "Transfer",
                StringComparison.OrdinalIgnoreCase)).ToList();
            if (transfers.Count != (resolved.Targets.Count == 0 ? 0 : 1))
                throw new CliException("K2 Form '" + definition.Name + "' test-only Pre-fill transfer action is missing or duplicated.");
            if (resolved.Targets.Count > 0)
            {
                var parameters = transfers[0].Descendants().Where(x => x.Name.LocalName == "Parameter").ToList();
                if (parameters.Count != resolved.Targets.Count)
                    throw new CliException("K2 Form '" + definition.Name + "' test-only Pre-fill target count is incorrect.");
                foreach (var target in resolved.Targets)
                {
                    var instance = FindInstance(form, target);
                    if (!parameters.Any(x =>
                        string.Equals((string)x.Attribute("TargetInstanceID"), instance, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)x.Attribute("TargetID"), target.ControlId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)x.Attribute("TargetType"), "Control", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(ChildValue(x, "SourceValue"), target.Value, StringComparison.Ordinal)))
                        throw new CliException("K2 Form '" + definition.Name +
                            "' test-only Pre-fill rule omits " + target.ViewName + "." + target.Property + ".");
                }
            }
            if (actions.Count(x => string.Equals((string)x.Attribute("Type"), "ShowMessage",
                StringComparison.OrdinalIgnoreCase)) != 1)
                throw new CliException("K2 Form '" + definition.Name +
                    "' test-only Pre-fill rule must finish with one warning message.");
        }

        internal static string Errata(FormDefinition definition)
        {
            return definition.PreFill.EffectiveEnabled
                ? "ERRATA test-only Pre-fill: enabled on Form '" + definition.Name +
                  "'. Remove before go-live by setting preFill.enabled=false with disabledReason and regenerating the Form."
                : "Pre-fill: disabled on Form '" + definition.Name + "' (" + definition.PreFill.DisabledReason + ").";
        }

        private static void AddRule(XElement form, FormDefinition definition,
            ResolvedFormPreFill resolved, string buttonId)
        {
            var ns = form.Name.Namespace;
            var state = RequiredChild(RequiredChild(form, "States"), "State");
            var events = state.Elements().FirstOrDefault(x => x.Name.LocalName == "Events");
            if (events == null) { events = new XElement(ns + "Events"); state.Add(events); }
            var actions = new XElement(ns + "Actions");
            if (resolved.Targets.Count > 0) actions.Add(BuildTransfer(form, resolved));
            actions.Add(BuildMessage(ns, resolved));
            events.Add(ControlRuleDefinition.BuildSystemEvent(ns, buttonId, "OnClick"));
            events.Add(new XElement(ns + "Event", new XAttribute("ID", NewId()),
                new XAttribute("DefinitionID", NewId()), new XAttribute("Type", "User"),
                new XAttribute("SourceID", buttonId), new XAttribute("SourceType", "Control"),
                new XAttribute("SourceName", ButtonName), new XAttribute("SourceDisplayName", ButtonName),
                new XElement(ns + "Name", "OnClick"),
                new XElement(ns + "Properties",
                    Property(ns, "RuleFriendlyName", "When " + ButtonText + " is Clicked", null, null),
                    Property(ns, "Location", definition.Name, null, null)),
                new XElement(ns + "Handlers",
                    new XElement(ns + "Handler", new XAttribute("ID", NewId()),
                        new XAttribute("DefinitionID", NewId()),
                        new XElement(ns + "Properties",
                            Property(ns, "HandlerName", "IfLogicalHandler", null, null),
                            Property(ns, "Location", "form", null, null)),
                        actions))));
        }

        private static XElement BuildTransfer(XElement form, ResolvedFormPreFill resolved)
        {
            var ns = form.Name.Namespace;
            var parameters = new XElement(ns + "Parameters");
            foreach (var target in resolved.Targets)
                parameters.Add(new XElement(ns + "Parameter",
                    new XAttribute("SourceID", "Sources"), new XAttribute("SourceType", "Value"),
                    new XAttribute("TargetInstanceID", FindInstance(form, target)),
                    new XAttribute("TargetID", target.ControlId),
                    new XAttribute("TargetName", target.ControlName),
                    new XAttribute("TargetDisplayName", target.ControlName),
                    new XAttribute("TargetType", "Control"),
                    new XElement(ns + "SourceValue", new XAttribute(XNamespace.Xml + "space", "preserve"),
                        target.Value ?? string.Empty)));
            return new XElement(ns + "Action", new XAttribute("ID", NewId()),
                new XAttribute("DefinitionID", NewId()), new XAttribute("Type", "Transfer"),
                new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    Property(ns, "Location", "Form", null, null),
                    Property(ns, "DesignTemplate", "ServerDataTransfer", null, null),
                    Property(ns, "FormID", (string)form.Attribute("ID"),
                        ChildValue(form, "Name"), ChildValue(form, "Name"))),
                parameters);
        }

        private static XElement BuildMessage(XNamespace ns, ResolvedFormPreFill resolved)
        {
            var body = resolved.Targets.Count + " field(s) filled with test data. Review every value before saving. ";
            body += resolved.ManualProperties.Count == 0
                ? "No manual-only fields were detected."
                : resolved.ManualProperties.Count + " field(s), including files or unsupported/empty lookups, still require manual input.";
            body += " This helper is test-only and must be removed before go-live.";
            return new XElement(ns + "Action", new XAttribute("ID", NewId()),
                new XAttribute("DefinitionID", NewId()), new XAttribute("Type", "ShowMessage"),
                new XAttribute("ExecutionType", "Synchronous"),
                new XElement(ns + "Properties",
                    Property(ns, "Location", "Form", null, null),
                    Property(ns, "MessageLocation", "Popup", null, null)),
                new XElement(ns + "Parameters",
                    MessageParameter(ns, "Size", "small"),
                    MessageParameter(ns, "Type", "warning"),
                    MessageParameter(ns, "Title", "Test data pre-filled"),
                    MessageParameter(ns, "Body", body)));
        }

        private static XElement MessageParameter(XNamespace ns, string target, string value)
        {
            return new XElement(ns + "Parameter", new XAttribute("SourceID", "Sources"),
                new XAttribute("SourceType", "Value"), new XAttribute("TargetID", target),
                new XAttribute("TargetName", target), new XAttribute("TargetType", "MessageProperty"),
                new XElement(ns + "SourceValue", new XAttribute(XNamespace.Xml + "space", "preserve"),
                    new XElement(ns + "Source", new XAttribute("SourceType", "Value"), value)));
        }

        private static XElement SelectTargetPanel(XElement form, FormDefinition definition)
        {
            var controls = RequiredChild(form, "Controls");
            var panels = RequiredChild(form, "Panels").Elements()
                .Where(x => x.Name.LocalName == "Panel").ToList();
            if (panels.Count == 0) throw new CliException("Generated Form has no layout panel for the Pre-fill button.");
            var visible = panels.Where(panel =>
            {
                var control = controls.Elements().FirstOrDefault(x => x.Name.LocalName == "Control" &&
                    string.Equals((string)x.Attribute("Type"), "Panel", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Attribute("ID"), (string)panel.Attribute("ID"),
                        StringComparison.OrdinalIgnoreCase));
                return control == null || !string.Equals(ReadProperty(control, "IsVisible"), "false",
                    StringComparison.OrdinalIgnoreCase);
            }).ToList();
            if (visible.Count == 0) return panels[0];
            return definition.GuidedJourney == null ? visible[visible.Count - 1] : visible[0];
        }

        private static string FindInstance(XElement form, ResolvedPreFillTarget target)
        {
            var item = form.Descendants().FirstOrDefault(x => x.Name.LocalName == "Item" &&
                (string.Equals((string)x.Attribute("ViewID"), target.ViewGuid.ToString(),
                     StringComparison.OrdinalIgnoreCase) ||
                 string.Equals((string)x.Attribute("ViewName"), target.ViewName,
                     StringComparison.OrdinalIgnoreCase)));
            var id = item == null ? null : (string)item.Attribute("ID");
            if (string.IsNullOrWhiteSpace(id))
                throw new CliException("Generated Form has no View instance for Pre-fill target '" +
                    target.ViewName + "." + target.Property + "'.");
            return id;
        }

        private static XElement RequiredChild(XElement parent, string name)
        {
            var children = parent.Elements().Where(x => x.Name.LocalName == name).ToList();
            if (children.Count != 1)
                throw new CliException("K2 Form definition requires exactly one " + name + " under " +
                    parent.Name.LocalName + ".");
            return children[0];
        }

        private static XElement Control(XNamespace ns, string id, string type, string name,
            params XElement[] extraProperties)
        {
            var properties = new XElement(ns + "Properties",
                Property(ns, "ControlName", name, name, name));
            foreach (var property in extraProperties) properties.Add(property);
            return new XElement(ns + "Control", new XAttribute("ID", id),
                new XAttribute("Type", type), new XElement(ns + "Name", name),
                new XElement(ns + "DisplayName", name), properties);
        }

        private static XElement Property(XNamespace ns, string name, string value,
            string displayValue, string nameValue)
        {
            var property = new XElement(ns + "Property", new XElement(ns + "Name", name));
            if (displayValue != null) property.Add(new XElement(ns + "DisplayValue", displayValue));
            if (nameValue != null) property.Add(new XElement(ns + "NameValue", nameValue));
            property.Add(new XElement(ns + "Value", value ?? string.Empty));
            return property;
        }

        private static string ReadProperty(XElement owner, string name)
        {
            var property = owner.Descendants().FirstOrDefault(x => x.Name.LocalName == "Property" &&
                string.Equals(ChildValue(x, "Name"), name, StringComparison.OrdinalIgnoreCase));
            return property == null ? null : ChildValue(property, "Value");
        }

        private static string ChildValue(XElement parent, string name)
        {
            var child = parent.Elements().FirstOrDefault(x => x.Name.LocalName == name);
            return child == null ? null : child.Value;
        }

        private static string NewId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
